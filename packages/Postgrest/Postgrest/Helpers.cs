using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Supabase.Core;
using Supabase.Core.Diagnostics;
using Supabase.Core.Extensions;
using Supabase.Core.Http;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;

[assembly: InternalsVisibleTo("Postgrest.Tests")]

namespace Supabase.Postgrest;


internal static class Helpers
{
    private static readonly HttpClient Client = new HttpClient();

    private static readonly Guid AppSession = Guid.NewGuid();

    /// <summary>
    /// Resolves the client a request should be sent through, once per <see cref="Postgrest.Client"/>/<see cref="Table{TModel}"/>
    /// construction: the caller-injected <see cref="ClientOptions.HttpClient"/>, else a proxy-configured client when
    /// <see cref="ClientOptions.Proxy"/> is set, else null (callers fall back to the shared default <see cref="Client"/>).
    /// </summary>
    internal static HttpClient? ResolveHttpClient(ClientOptions options) =>
        options.HttpClient ?? (options.Proxy != null ? DefaultHttpClientFactory.Create(proxy: options.Proxy) : null);

    /// <summary>
    /// Mirrors Newtonsoft's <c>JToken.HasValues</c>: true only when the serialized payload is a non-empty
    /// object or array. An empty body (e.g. an update whose columns were all dropped) is not sent.
    /// </summary>
    private static bool HasValues(string json)
    {
        var node = JsonNode.Parse(json);
        return node switch
        {
            JsonObject obj => obj.Count > 0,
            JsonArray array => array.Count > 0,
            _ => false
        };
    }

    /// <summary>
    /// Helper to make a request using the defined parameters to an API Endpoint and coerce into a model.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="clientOptions"></param>
    /// <param name="httpClient">The resolved client to send through. Defaults to a shared client when null.</param>
    /// <param name="method"></param>
    /// <param name="url"></param>
    /// <param name="data"></param>
    /// <param name="headers"></param>
    /// <param name="serializerSettings"></param>
    /// <param name="getHeaders"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="operation">Logical operation name recorded on telemetry (select/insert/update/…).</param>
    /// <returns></returns>
    public static async Task<ModeledResponse<T>> MakeRequestAsync<T>(ClientOptions clientOptions, HttpClient? httpClient, HttpMethod method, string url, JsonSerializerOptions serializerSettings, object? data = null,
        Dictionary<string, string>? headers = null, Func<Dictionary<string, string>>? getHeaders = null, CancellationToken cancellationToken = default, string? operation = null) where T : BaseModel, new()
    {
        var baseResponse = await MakeRequestAsync(clientOptions, httpClient, method, url, serializerSettings, data, headers, cancellationToken, operation);
        return new ModeledResponse<T>(baseResponse, serializerSettings, getHeaders);
    }

    /// <summary>
    /// Helper to make a request using the defined parameters to an API Endpoint.
    /// </summary>
    /// <param name="clientOptions"></param>
    /// <param name="httpClient">The resolved client to send through. Defaults to a shared client when null.</param>
    /// <param name="method"></param>
    /// <param name="url"></param>
    /// <param name="data"></param>
    /// <param name="headers"></param>
    /// <param name="serializerSettings"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="operation">Logical operation name recorded on telemetry (select/insert/update/…).</param>
    /// <returns></returns>
    public static async Task<BaseResponse> MakeRequestAsync(ClientOptions clientOptions, HttpClient? httpClient, HttpMethod method, string url, JsonSerializerOptions serializerSettings, object? data = null,
        Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default, string? operation = null)
    {
        var builder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(builder.Query);

        if (data != null && method == HttpMethod.Get)
        {
            // Case if it's a Get request the data object is a dictionary<string,string>
            if (data is Dictionary<string, string> reqParams)
            {
                foreach (var param in reqParams)
                    query[param.Key] = param.Value;
            }
        }

        builder.Query = query.ToString();

        string? body = null;

        if (data != null && method != HttpMethod.Get)
        {
            var stringContent = JsonSerializer.Serialize(data, serializerSettings);

            if (!string.IsNullOrWhiteSpace(stringContent) && HasValues(stringContent))
            {
                body = stringContent;
            }
        }

        HttpRequestMessage CreateRequest()
        {
            var requestMessage = new HttpRequestMessage(method, builder.Uri);

            if (body != null)
            {
                requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    requestMessage.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }

            return requestMessage;
        }

        using var activity = PostgrestInstrumentation.StartHttpActivity(method, builder.Uri, operation);
        var startTimestamp = Stopwatch.GetTimestamp();
        int? statusCode = null;
        string? errorType = null;

        try
        {
            using var response = await RetryExecutor.SendAsync(httpClient ?? Client, CreateRequest, clientOptions.Retry, cancellationToken).ConfigureAwait(false);
            statusCode = (int) response.StatusCode;
            activity.SetHttpResponseTags(statusCode.Value);

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return new BaseResponse(clientOptions, response, content);

            errorType = statusCode.Value.ToString();
            var exception = new PostgrestException(content)
            {
                Content = content,
                Response = response,
                StatusCode = (int) response.StatusCode
            };
            exception.AddReason();
            throw exception;
        }
        catch (Exception e) when (e is not PostgrestException)
        {
            // Transport-level failures (no response); Postgrest surfaces these raw, so tag and rethrow.
            errorType = e.GetType().FullName;
            activity.SetFailure(e);
            throw;
        }
        finally
        {
            PostgrestInstrumentation.RecordRequest(method, builder.Uri, operation, statusCode, errorType, startTimestamp);
        }
    }

    /// <summary>
    /// Prepares the request with appropriate HTTP headers expected by Postgrest.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="headers"></param>
    /// <param name="options"></param>
    /// <param name="rangeFrom"></param>
    /// <param name="rangeTo"></param>
    /// <returns></returns>
    public static Dictionary<string, string> PrepareRequestHeaders(HttpMethod method, Dictionary<string, string>? headers = null, ClientOptions? options = null, int rangeFrom = int.MinValue, int rangeTo = int.MinValue)
    {
        options ??= new ClientOptions();

        headers = headers == null ? new Dictionary<string, string>(options.Headers) : options.Headers.MergeLeft(headers);

        if (!string.IsNullOrEmpty(options.Schema))
        {
            headers.Add(method == HttpMethod.Get ? "Accept-Profile" : "Content-Profile", options.Schema);
        }

        if (rangeFrom != int.MinValue)
        {
            var formatRangeTo = rangeTo != int.MinValue ? rangeTo.ToString() : null;

            headers.Add("Range-Unit", "items");
            headers.Add("Range", $"{rangeFrom}-{formatRangeTo}");
        }

        if (!headers.ContainsKey("X-Client-Info"))
        {
            try
            {
                // Default version to match other clients
                // https://github.com/search?q=org%3Asupabase-community+x-client-info&type=code
                headers.Add("X-Client-Info", Util.GetAssemblyVersion(typeof(Client)));
            }
            catch (Exception)
            {
                // Fallback for when the version can't be found
                // e.g. running in the Unity Editor, ILL2CPP builds, etc.
                headers.Add("X-Client-Info", $"supabase.postgrest-csharp/session-{AppSession}");
            }
        }

        return headers;
    }
}
