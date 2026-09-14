using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Supabase.Core;
using Supabase.Core.Diagnostics;
using Supabase.Core.Extensions;
using Supabase.Core.Http;
using Supabase.Functions.Exceptions;
using Supabase.Functions.Interfaces;

[assembly: InternalsVisibleTo("Functions.Tests")]

namespace Supabase.Functions;

/// <inheritdoc />
public partial class Client : IFunctionsClient
{
    /// <summary>
    /// Serialization settings tuned to match the previous Newtonsoft.Json behavior: case-insensitive
    /// property matching on deserialize, and relaxed escaping so characters such as '+', '&amp;' and
    /// non-ASCII are written literally rather than as \u escapes.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly FunctionRegion region;

    /// <summary>
    /// Function that can be set to return dynamic headers.
    ///
    /// Headers specified in the method parameters will ALWAYS take precedence over headers returned by this function.
    /// </summary>
    public Func<Dictionary<string, string>>? GetHeaders { get; set; }

    /// <summary>The options this client was constructed with.</summary>
    public ClientOptions Options { get; }

    /// <summary>
    /// Initializes a functions client
    /// </summary>
    /// <param name="baseUrl"></param>
    /// <param name="region"></param>
    /// <param name="options"></param>
    public Client(string baseUrl, FunctionRegion? region = null, ClientOptions? options = null)
    {
        this.baseUrl = baseUrl;
        this.region = region ?? FunctionRegion.Any;
        this.Options = options ?? new ClientOptions();
        this.httpClient = this.Options.HttpClient ?? DefaultHttpClientFactory.Create(proxy: this.Options.Proxy);
    }

    /// <summary>
    /// Returns an <see cref="HttpContent"/> response, allowing for coersion into Streams, Strings, and byte[]
    /// </summary>
    /// <param name="functionName">Function Name, will be appended to BaseUrl</param>
    /// <param name="token">Anon Key.</param>
    /// <param name="options">Options</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<HttpContent> RawInvoke(
        string functionName,
        string? token = null,
        InvokeFunctionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"{this.baseUrl}/{functionName}";

        return (await this.HandleRequest(functionName, url, token, options, cancellationToken)).Content;
    }

    /// <summary>
    /// Invokes a function and returns a successful response after its headers arrive.
    /// <see cref="InvokeFunctionOptions.HttpTimeout"/> covers the request and any error body.
    /// </summary>
    /// <param name="functionName">Function name, appended to the base URL.</param>
    /// <param name="token">Bearer token.</param>
    /// <param name="options">Invocation options.</param>
    /// <param name="cancellationToken">Cancels the request and error-body reads. Cancel successful body reads separately.</param>
    /// <returns>The response, which the caller must dispose.</returns>
    public Task<HttpResponseMessage> InvokeStream(
        string functionName,
        string? token = null,
        InvokeFunctionOptions? options = null,
        CancellationToken cancellationToken = default
    ) => this.HandleRequest(functionName, $"{this.baseUrl}/{functionName}", token, options, cancellationToken, HttpCompletionOption.ResponseHeadersRead);

    /// <summary>
    /// Invokes a function and returns the Text content of the response.
    /// </summary>
    /// <param name="functionName">Function Name, will be appended to BaseUrl</param>
    /// <param name="token">Anon Key.</param>
    /// <param name="options">Options</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> Invoke(
        string functionName,
        string? token = null,
        InvokeFunctionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"{this.baseUrl}/{functionName}";
        var response = await this.HandleRequest(functionName, url, token, options, cancellationToken);

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Invokes a function and returns a JSON Deserialized object according to the supplied generic Type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="functionName">Function Name, will be appended to BaseUrl</param>
    /// <param name="token">Anon Key.</param>
    /// <param name="options">Options</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<T?> Invoke<T>(
        string functionName,
        string? token = null,
        InvokeFunctionOptions? options = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var url = $"{this.baseUrl}/{functionName}";
        var response = await this.HandleRequest(functionName, url, token, options, cancellationToken);

        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(content, SerializerOptions);
    }

    /// <summary>
    /// Internal request handling
    /// </summary>
    /// <param name="functionName"></param>
    /// <param name="url"></param>
    /// <param name="token"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="completionOption"></param>
    /// <returns></returns>
    /// <exception cref="FunctionsException"></exception>
    private async Task<HttpResponseMessage> HandleRequest(
        string functionName,
        string url,
        string? token = null,
        InvokeFunctionOptions? options = null,
        CancellationToken cancellationToken = default,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead
    )
    {
        options ??= new InvokeFunctionOptions();

        if (this.GetHeaders != null)
        {
            options.Headers = this.GetHeaders().MergeLeft(options.Headers);
        }

        if (!string.IsNullOrEmpty(token))
        {
            options.Headers["Authorization"] = $"Bearer {token}";
        }

        options.Headers["X-Client-Info"] = Util.GetAssemblyVersion(typeof(Client));

        var region = options.FunctionRegion ?? this.region;
        if (region != FunctionRegion.Any)
        {
            options.Headers["x-region"] = region.ToString();
        }

        var uri = BuildUri(url);

        using var activity = FunctionsInstrumentation.StartInvokeActivity(options.HttpMethod, uri, functionName);
        var startTimestamp = Stopwatch.GetTimestamp();
        int? statusCode = null;
        string? errorType = null;

        using var timeoutCts = new CancellationTokenSource(options.HttpTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var response = await RetryExecutor.SendAsync(this.httpClient, () => BuildRequestMessage(options, uri), this.Options.Retry, completionOption, linkedCts.Token);
            statusCode = (int) response.StatusCode;
            var isRelayError = response.Headers.Contains("x-relay-error");
            activity.SetHttpResponseTags(statusCode.Value);

            if (response.IsSuccessStatusCode && !isRelayError)
                return response;

            // A relay error is a failure even when the status code is a success, so it is not
            // covered by SetHttpResponseTags' status-code check above.
            if (isRelayError)
            {
                errorType = "x-relay-error";
                activity?.SetTag("error.type", errorType);
                activity?.SetStatus(ActivityStatusCode.Error);
            }
            else
            {
                errorType = statusCode.Value.ToString();
            }

            var content = completionOption == HttpCompletionOption.ResponseContentRead
                ? await response.Content.ReadAsStringAsync()
                : await BufferErrorBody(response, linkedCts.Token);
            var exception = new FunctionsException(content)
            {
                Content = content,
                Response = response,
                StatusCode = (int) response.StatusCode,
            };
            exception.AddReason();
            throw exception;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var timeoutException = new TimeoutException($"The request timed out after {options.HttpTimeout}.");
            errorType = timeoutException.GetType().FullName;
            activity.SetFailure(timeoutException);
            throw new TaskCanceledException(timeoutException.Message, timeoutException, cancellationToken);
        }
        catch (Exception e) when (e is not FunctionsException)
        {
            // Transport-level failures (no response); Functions surfaces these raw, so tag and rethrow.
            errorType = e.GetType().FullName;
            activity.SetFailure(e);
            throw;
        }
        finally
        {
            FunctionsInstrumentation.RecordInvoke(options.HttpMethod, uri, functionName, statusCode, errorType, startTimestamp);
        }
    }

    // Keep the error body readable through FunctionsException.Response.
    private static async Task<string> BufferErrorBody(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var buffer = new MemoryStream();
            var stream = await response.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(buffer, cancellationToken);

            var buffered = new ByteArrayContent(buffer.ToArray());
            foreach (var header in response.Content.Headers)
                buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);

            response.Content.Dispose();
            response.Content = buffered;

            return await buffered.ReadAsStringAsync();
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static Uri BuildUri(string url)
    {
        var builder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(builder.Query);
        builder.Query = query.ToString();
        return builder.Uri;
    }

    private static HttpRequestMessage BuildRequestMessage(InvokeFunctionOptions options, Uri uri)
    {
        var request = new HttpRequestMessage(options.HttpMethod, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(options.Body, SerializerOptions), Encoding.UTF8, "application/json")
        };

        foreach (var kvp in options.Headers)
        {
            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        return request;
    }
}
