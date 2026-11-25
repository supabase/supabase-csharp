using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Exceptions;

namespace Supabase.Storage.Common;

internal sealed class VectorService
{
    private readonly string url;
    private readonly ClientOptions options;
    private readonly HttpClient requestClient;
    private readonly Func<Dictionary<string, string>> headers;

    internal VectorService(string storageUrl, ClientOptions options, HttpClient requestClient, Func<Dictionary<string, string>> headers)
    {
        this.url = $"{storageUrl.TrimEnd('/')}/vector";
        this.options = options;
        this.requestClient = requestClient;
        this.headers = headers;
    }

    internal Task Post(string operation, object body, CancellationToken cancellationToken) =>
        Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Post, $"{this.url}/{operation}", body, this.headers(), cancellationToken);

    internal async Task<TResponse> PostAsync<TResponse>(string operation, object body, CancellationToken cancellationToken)
        where TResponse : class =>
        await Helpers.MakeRequestAsync<TResponse>(this.requestClient, this.options, HttpMethod.Post, $"{this.url}/{operation}", body, this.headers(), cancellationToken).ConfigureAwait(false)
            ?? throw new SupabaseStorageException($"The vector service returned an empty {operation} response.");

    internal static void RequireName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The value cannot be null, empty or whitespace.", parameterName);
    }
}
