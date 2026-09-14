#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Supabase.Core.Diagnostics;
using Supabase.Core.Http;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Responses;

#endregion

namespace Supabase.Gotrue;

/// <summary>
/// Utility methods to assist with flow. Includes nonce generation and verification.
/// </summary>
public static class Helpers
{
    /// <summary>
    /// Serialization settings tuned to match the previous Newtonsoft.Json behavior: case-insensitive
    /// property matching on deserialize, and relaxed escaping so characters such as '+', '&amp;' and
    /// non-ASCII are written literally rather than as \u escapes. Shared by every serialize/deserialize
    /// call in the package.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new Serialization.ObjectToInferredTypesConverter() },
    };

    private static readonly HttpClient Client = new HttpClient();
    /// <summary>
    /// Generates a nonce (code verifier)
    /// Used with PKCE flow and Apple/Google Sign in.
    /// Paired with <see cref="GeneratePKCENonceVerifier(string)"/>
    ///
    /// Sourced from: https://stackoverflow.com/a/65220376/3629438
    /// </summary>
    public static string GenerateNonce()
    {
        // ReSharper disable once StringLiteralTypo
        const string chars = "abcdefghijklmnopqrstuvwxyz123456789";
        var nonce = new char[128];
        for (var i = 0; i < nonce.Length; i++)
        {
            nonce[i] = chars[RandomNumberGenerator.GetInt32(0, chars.Length)];
        }

        return new string(nonce);
    }

    /// <summary>
    /// Generates a PKCE SHA256 code challenge given a nonce (code verifier)
    /// 
    /// Paired with <see cref="GenerateNonce"/>
    ///
    /// Sourced from: https://stackoverflow.com/a/65220376/3629438
    /// </summary>
    /// <param name="codeVerifier"></param>
    public static string GeneratePKCENonceVerifier(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var b64Hash = Convert.ToBase64String(hash);
        var code = Regex.Replace(b64Hash, "\\+", "-");
        code = Regex.Replace(code, "\\/", "_");
        code = Regex.Replace(code, "=+$", "");
        return code;
    }

    /// <summary>
    /// Generates a SHA256 nonce given a rawNonce, used Apple/Google Sign in.
    /// </summary>
    /// <param name="rawNonce"></param>
    /// <returns></returns>
    public static string GenerateSHA256NonceFromRawNonce(string rawNonce)
    {
        var sha = new SHA256Managed();
        var utf8RawNonce = Encoding.UTF8.GetBytes(rawNonce);
        var hash = sha.ComputeHash(utf8RawNonce);

        var result = string.Empty;
        foreach (var t in hash)
            result += t.ToString("x2");

        return result;
    }

    /// <summary>
    /// Generates the relevant login URL for a third-party provider.
    ///
    /// Modeled after: https://github.com/supabase/auth-js/blob/92fefbd49f25e20793ca74d5b83142a1bb805a18/src/GoTrueClient.ts#L2294-L2332
    /// </summary>
    /// <param name="url"></param>
    /// <param name="provider"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    internal static ProviderAuthState GetUrlForProvider(string url, Constants.Provider provider, SignInOptions? options = null)
    {
        var builder = new UriBuilder(url);
        var result = new ProviderAuthState(builder.Uri);

        var attr = Core.Helpers.GetMappedToAttr(provider);
        var query = HttpUtility.ParseQueryString("");
        options ??= new SignInOptions();

        if (options.FlowType == Constants.OAuthFlowType.PKCE)
        {
            var codeVerifier = GenerateNonce();
            var codeChallenge = GeneratePKCENonceVerifier(codeVerifier);

            query.Add("flow_type", "pkce");
            query.Add("code_challenge", codeChallenge);
            query.Add("code_challenge_method", "s256");

            result.PKCEVerifier = codeVerifier;
        }

        if (attr == null)
            throw new Exception("Unknown provider");

        // The OAuth `state` is generated and validated by the GoTrue server (its flow_state);
        // it is not a client parameter. Sending our own collides with that round-trip and makes
        // sign-in fail with `bad_oauth_state` (issue #377), so we no longer add it — matching
        // auth-js. For server-side CSRF correlation, carry a token on `RedirectTo`, which GoTrue
        // echoes back to your callback.
        query.Add("provider", attr.Mapping);

        if (!string.IsNullOrEmpty(options.Scopes))
            query.Add("scopes", options.Scopes);

        if (!string.IsNullOrEmpty(options.RedirectTo))
            query.Add("redirect_to", options.RedirectTo);

        if (options.QueryParams != null)
            foreach (var param in options.QueryParams)
                query[param.Key] = param.Value;

        builder.Query = query.ToString();

        result.Uri = builder.Uri;
        return result;
    }

    /// <summary>
    /// Adds query params to a given Url
    /// </summary>
    /// <param name="url"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    internal static Uri AddQueryParams(string url, Dictionary<string, string> data)
    {
        var builder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(builder.Query);

        foreach (var param in data)
            query[param.Key] = param.Value;

        builder.Query = query.ToString();

        return builder.Uri;
    }

    /// <summary>
    /// Helper to make a request using the defined parameters to an API Endpoint and coerce into a model.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="method"></param>
    /// <param name="url"></param>
    /// <param name="data"></param>
    /// <param name="headers"></param>
    /// <param name="httpClient">The client to send through. Defaults to a shared client when null.</param>
    /// <param name="retry">Retry policy to apply. Defaults to no retries.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal static async Task<T?> MakeRequestAsync<T>(HttpMethod method, string url, object? data = null, Dictionary<string, string>? headers = null,
        HttpClient? httpClient = null, RetryOptions? retry = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var baseResponse = await MakeRequestAsync(method, url, data, headers, httpClient, retry, cancellationToken);
        return baseResponse.Content != null ? JsonSerializer.Deserialize<T>(baseResponse.Content, SerializerOptions) : default;
    }

    /// <summary>
    /// Helper to make a request using the defined parameters to an API Endpoint.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="url"></param>
    /// <param name="data"></param>
    /// <param name="headers"></param>
    /// <param name="httpClient">The client to send through. Defaults to a shared client when null.</param>
    /// <param name="retry">Retry policy to apply. Defaults to no retries.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal static async Task<BaseResponse> MakeRequestAsync(HttpMethod method, string url, object? data = null, Dictionary<string, string>? headers = null,
        HttpClient? httpClient = null, RetryOptions? retry = null, CancellationToken cancellationToken = default)
    {
        var uri = BuildUri(url, method, data);

        using var activity = GotrueInstrumentation.StartHttpActivity(method, uri);
        var startTimestamp = Stopwatch.GetTimestamp();
        int? statusCode = null;
        string? errorType = null;

        try
        {
            using var response = await RetryExecutor.SendAsync(httpClient ?? Client, () => BuildRequestMessage(method, uri, data, headers),
                retry ?? new RetryOptions(), cancellationToken).ConfigureAwait(false);
            statusCode = (int) response.StatusCode;
            activity.SetHttpResponseTags(statusCode.Value);

            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                errorType = statusCode.Value.ToString();
                var e = new GotrueException(content ?? "Request Failed")
                {
                    Content = content,
                    Response = response,
                    StatusCode = (int) response.StatusCode
                };
                e.AddReason();
                throw e;
            }
            return new BaseResponse { Content = content, ResponseMessage = response };
        }
        catch (HttpRequestException hre)
        {
            errorType = hre.GetType().FullName;
            activity.SetFailure(hre);
            throw new GotrueException(hre.Message, FailureHint.Reason.Offline, hre);
        }
        finally
        {
            GotrueInstrumentation.RecordRequest(method, uri, statusCode, errorType, startTimestamp);
        }
    }

    private static Uri BuildUri(string url, HttpMethod method, object? data)
    {
        var builder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(builder.Query);

        // Case if it's a Get request the data object is a dictionary<string,string>
        if (method == HttpMethod.Get && data is Dictionary<string, string> reqParams)
        {
            foreach (var param in reqParams)
                query[param.Key] = param.Value;
        }

        builder.Query = query.ToString();
        return builder.Uri;
    }

    private static HttpRequestMessage BuildRequestMessage(HttpMethod method, Uri uri, object? data, Dictionary<string, string>? headers)
    {
        var request = new HttpRequestMessage(method, uri);

        if (data != null && method != HttpMethod.Get)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(data, SerializerOptions), Encoding.UTF8, "application/json");
        }

        if (headers != null)
        {
            foreach (var kvp in headers)
            {
                request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
        }

        return request;
    }
}
