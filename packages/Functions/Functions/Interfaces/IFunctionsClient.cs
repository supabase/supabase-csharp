using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Interfaces;

namespace Supabase.Functions.Interfaces;

/// <summary>
/// Represents a contract for a Supabase Functions Client
/// </summary>
public interface IFunctionsClient : IGettableHeaders
{
    /// <summary>
    /// Invokes a function given a URL and access token. Returns the string content.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="token"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> Invoke(string url, string? token = null, Client.InvokeFunctionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a function given a URL and access token. Returns a typed response (should be a JSON.net parsable object)
    /// </summary>
    /// <param name="url"></param>
    /// <param name="token"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<T?> Invoke<T>(string url, string? token = null, Client.InvokeFunctionOptions? options = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Invokes a function given a URL and access token. Returns the raw HTTP response.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="token"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<HttpContent> RawInvoke(string url, string? token = null, Client.InvokeFunctionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a function and returns a successful response after its headers arrive.
    /// </summary>
    /// <param name="url">Function name, appended to the base URL.</param>
    /// <param name="token">Bearer token.</param>
    /// <param name="options">Invocation options.</param>
    /// <param name="cancellationToken">Cancels the request and error-body reads. Cancel successful body reads separately.</param>
    /// <returns>The response, which the caller must dispose.</returns>
    Task<HttpResponseMessage> InvokeStream(string url, string? token = null, Client.InvokeFunctionOptions? options = null, CancellationToken cancellationToken = default);
}
