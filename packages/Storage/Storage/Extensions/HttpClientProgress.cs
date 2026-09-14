using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BirdMessenger;
using BirdMessenger.Collections;
using BirdMessenger.Events;
using BirdMessenger.Infrastructure;
using Supabase.Core.Diagnostics;
using Supabase.Storage.Exceptions;

namespace Supabase.Storage.Extensions;

/// <summary>
/// Adapted from: https://gist.github.com/dalexsoto/9fd3c5bdbe9f61a717d47c5843384d11
/// </summary>
internal static class HttpClientProgress
{
    /// <summary>
    /// Buffer size for stream copies, matching the BCL's default <see cref="Stream.CopyTo(Stream)"/>
    /// size (80 KB). Kept just under the 85,000-byte Large Object Heap threshold so the buffer
    /// stays on the gen-0 heap. This is set to the default expected value.
    /// </summary>
    private const int CopyBufferSize = 81920;

    public static async Task<MemoryStream> DownloadDataAsync(
        this HttpClient client,
        Uri uri,
        Dictionary<string, string>? headers = null,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var destination = new MemoryStream();
        var message = new HttpRequestMessage(HttpMethod.Get, uri);

        if (headers != null)
        {
            foreach (var header in headers)
            {
                message.Headers.Add(header.Key, header.Value);
            }
        }

        using var activity = StorageInstrumentation.StartHttpActivity(HttpMethod.Get, uri, StorageInstrumentation.DirectionDownload);
        var startTimestamp = Stopwatch.GetTimestamp();
        int? statusCode = null;
        string? errorType = null;

        try
        {
            using (
                var response = await client.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                )
            )
            {
                statusCode = (int) response.StatusCode;
                activity.SetHttpResponseTags(statusCode.Value);

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var errorResponse = ErrorResponse.TryParse(content);
                    var resolvedStatus = errorResponse?.StatusCode ?? (int) response.StatusCode;
                    errorType = resolvedStatus.ToString();
                    var e = new SupabaseStorageException(errorResponse?.Message ?? content)
                    {
                        Content = content,
                        Response = response,
                        StatusCode = resolvedStatus,
                        Code = errorResponse?.Code,
                    };

                    e.AddReason();
                    throw e;
                }

                var contentLength = response.Content.Headers.ContentLength;
                using (var download = await response.Content.ReadAsStreamAsync())
                {
                    if (progress is null || !contentLength.HasValue)
                    {
                        await download.CopyToAsync(destination, CopyBufferSize, cancellationToken);
                        return destination;
                    }

                    // Such progress and contentLength much reporting Wow!
                    var progressWrapper = new Progress<long>(totalBytes =>
                        progress.Report(GetProgressPercentage(totalBytes, contentLength.Value))
                    );
                    await download.CopyToAsync(
                        destination,
                        CopyBufferSize,
                        progressWrapper,
                        cancellationToken
                    );
                }
            }

            return destination;
        }
        catch (Exception e) when (!(e is SupabaseStorageException))
        {
            errorType = e.GetType().FullName;
            activity.SetFailure(e);
            throw;
        }
        finally
        {
            StorageInstrumentation.RecordTransfer(StorageInstrumentation.DirectionDownload, HttpMethod.Get, uri, destination.Length, statusCode, errorType, startTimestamp);
        }

        float GetProgressPercentage(float totalBytes, float currentBytes) =>
            (totalBytes / currentBytes) * 100f;
    }

    private static async Task CopyToAsync(
        this Stream source,
        Stream destination,
        int bufferSize,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        if (bufferSize < 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (!source.CanRead)
            throw new InvalidOperationException($"'{nameof(source)}' is not readable.");
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (!destination.CanWrite)
            throw new InvalidOperationException($"'{nameof(destination)}' is not writable.");

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;

        while (
            (
                bytesRead = await source
                    .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false)
            ) != 0
        )
        {
            await destination
                .WriteAsync(buffer, 0, bytesRead, cancellationToken)
                .ConfigureAwait(false);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }
    }

    public static Task<HttpResponseMessage> UploadFileAsync(
        this HttpClient client,
        Uri uri,
        string filePath,
        Dictionary<string, string>? headers = null,
        Progress<float>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var fileStream = new FileStream(filePath, mode: FileMode.Open, FileAccess.Read);
        return UploadAsync(client, uri, fileStream, headers, progress, cancellationToken);
    }

    public static Task<HttpResponseMessage> UploadBytesAsync(
        this HttpClient client,
        Uri uri,
        byte[] data,
        Dictionary<string, string>? headers = null,
        Progress<float>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var stream = new MemoryStream(data);
        return UploadAsync(client, uri, stream, headers, progress, cancellationToken);
    }

    public static async Task<HttpResponseMessage> UploadAsync(
        this HttpClient client,
        Uri uri,
        Stream stream,
        Dictionary<string, string>? headers = null,
        Progress<float>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var content = new ProgressableStreamContent(stream, 4096, progress);

        if (headers != null)
        {
            client.DefaultRequestHeaders.Clear();

            foreach (var header in headers)
            {
                if (header.Key.Contains("content"))
                    content.Headers.Add(header.Key, header.Value);
                else
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var bytes = stream.CanSeek ? stream.Length : (long?) null;
        using var activity = StorageInstrumentation.StartHttpActivity(HttpMethod.Post, uri, StorageInstrumentation.DirectionUpload);
        var startTimestamp = Stopwatch.GetTimestamp();
        int? statusCode = null;
        string? errorType = null;

        try
        {
            var response = await client.PostAsync(uri, content, cancellationToken);
            statusCode = (int) response.StatusCode;
            activity.SetHttpResponseTags(statusCode.Value);

            if (!response.IsSuccessStatusCode)
            {
                var httpContent = await response.Content.ReadAsStringAsync();
                var errorResponse = ErrorResponse.TryParse(httpContent);
                var resolvedStatus = errorResponse?.StatusCode ?? (int) response.StatusCode;
                errorType = resolvedStatus.ToString();
                var e = new SupabaseStorageException(errorResponse?.Message ?? httpContent)
                {
                    Content = httpContent,
                    Response = response,
                    StatusCode = resolvedStatus,
                    Code = errorResponse?.Code,
                };

                e.AddReason();
                throw e;
            }

            return response;
        }
        catch (Exception e) when (!(e is SupabaseStorageException))
        {
            errorType = e.GetType().FullName;
            activity.SetFailure(e);
            throw;
        }
        finally
        {
            StorageInstrumentation.RecordTransfer(StorageInstrumentation.DirectionUpload, HttpMethod.Post, uri, bytes, statusCode, errorType, startTimestamp);
        }
    }

    public static Task<HttpResponseMessage> UploadOrContinueFileAsync(
        this HttpClient client,
        Uri uri,
        string filePath,
        MetadataCollection metadata,
        Dictionary<string, string>? headers = null,
        Progress<float>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var fileStream = new FileStream(filePath, mode: FileMode.Open, FileAccess.Read);
        return ResumableUploadAsync(
            client,
            uri,
            fileStream,
            metadata,
            headers,
            progress,
            cancellationToken
        );
    }

    public static Task<HttpResponseMessage> UploadOrContinueByteAsync(
        this HttpClient client,
        Uri uri,
        byte[] data,
        MetadataCollection metadata,
        Dictionary<string, string>? headers = null,
        Progress<float>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var stream = new MemoryStream(data);
        return ResumableUploadAsync(
            client,
            uri,
            stream,
            metadata,
            headers,
            progress,
            cancellationToken
        );
    }

    private static async Task<HttpResponseMessage> ResumableUploadAsync(
        this HttpClient client,
        Uri uri,
        Stream fileStream,
        MetadataCollection metadata,
        Dictionary<string, string>? headers = null,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));

        if (fileStream.Position != 0 && fileStream.CanSeek)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        if (headers != null)
        {
            client.DefaultRequestHeaders.Clear();
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        // The resumable (TUS) upload is several HTTP requests (create + patched chunks) driven by
        // BirdMessenger, so this is one operation span for the whole transfer rather than a span
        // per underlying request.
        var bytes = fileStream.CanSeek ? fileStream.Length : (long?) null;
        using var activity = StorageInstrumentation.StartHttpActivity(HttpMethod.Post, uri, StorageInstrumentation.DirectionUpload);
        var startTimestamp = Stopwatch.GetTimestamp();
        int? statusCode = null;
        string? errorType = null;

        try
        {
            var cacheKey =
                $"{metadata["bucketName"]}/{metadata["objectName"]}/{metadata["contentType"]}";

            UploadMemoryCache.TryGet(cacheKey, out var upload);
            Uri? fileLocation = null;
            if (upload == null)
            {
                var createOption = new TusCreateRequestOption()
                {
                    Endpoint = uri,
                    Metadata = metadata,
                    UploadLength = fileStream.Length,
                };

                try
                {
                    var responseCreate = await client.TusCreateAsync(
                        createOption,
                        cancellationToken
                    );

                    fileLocation = responseCreate.FileLocation;
                    UploadMemoryCache.Set(cacheKey, fileLocation.ToString());
                }
                catch (TusException error)
                {
                    statusCode = (int) error.OriginHttpResponse.StatusCode;
                    errorType = statusCode.Value.ToString();
                    activity.SetHttpResponseTags(statusCode.Value);
                    throw await HandleResponseError(error);
                }
            }

            if (upload != null)
                fileLocation = new Uri(upload);

            var patchOption = new TusPatchRequestOption
            {
                FileLocation = fileLocation,
                Stream = fileStream,
                UploadBufferSize = 6 * 1024 * 1024,
                UploadType = UploadType.Chunk,
                OnProgressAsync = x => ReportProgressAsync(progress, x),
                OnCompletedAsync = _ =>
                {
                    UploadMemoryCache.Remove(cacheKey);
                    return Task.CompletedTask;
                },
                OnFailedAsync = _ => Task.CompletedTask,
            };

            var responsePatch = await client.TusPatchAsync(patchOption, cancellationToken);
            statusCode = (int) responsePatch.OriginResponseMessage.StatusCode;
            activity.SetHttpResponseTags(statusCode.Value);

            if (responsePatch.OriginResponseMessage.IsSuccessStatusCode)
                return responsePatch.OriginResponseMessage;

            errorType = statusCode.Value.ToString();
            throw await HandleResponseError(responsePatch.OriginResponseMessage);
        }
        catch (Exception e) when (!(e is SupabaseStorageException))
        {
            errorType = e.GetType().FullName;
            activity.SetFailure(e);
            throw;
        }
        finally
        {
            StorageInstrumentation.RecordTransfer(StorageInstrumentation.DirectionUpload, HttpMethod.Post, uri, bytes, statusCode, errorType, startTimestamp);
        }
    }

    private static Task ReportProgressAsync(
        IProgress<float>? progress,
        UploadProgressEvent progressInfo
    )
    {
        if (progress == null || progressInfo.TotalSize == null)
            return Task.CompletedTask;

        var uploadedProgress = (float) progressInfo.UploadedSize / progressInfo.TotalSize.Value * 100f;
        progress.Report(uploadedProgress);

        return Task.CompletedTask;
    }

    private static async Task<SupabaseStorageException> HandleResponseError(
        HttpResponseMessage response
    )
    {
        var httpContent = await response.Content.ReadAsStringAsync();
        var errorResponse = ErrorResponse.TryParse(httpContent);
        var error = new SupabaseStorageException(errorResponse?.Message ?? httpContent)
        {
            Content = httpContent,
            Response = response,
            StatusCode = errorResponse?.StatusCode ?? (int) response.StatusCode,
            Code = errorResponse?.Code,
        };
        error.AddReason();

        return error;
    }

    private static async Task<SupabaseStorageException> HandleResponseError(
        TusException response
    )
    {
        var httpContent = await response.OriginHttpResponse.Content.ReadAsStringAsync();
        var errorResponse = ErrorResponse.TryParse(httpContent);
        var error = new SupabaseStorageException(httpContent)
        {
            Content = httpContent,
            Response = response.OriginHttpResponse,
            StatusCode = (int) response.OriginHttpResponse.StatusCode,
            Code = errorResponse?.Code,
        };
        error.AddReason();

        return error;
    }
}
