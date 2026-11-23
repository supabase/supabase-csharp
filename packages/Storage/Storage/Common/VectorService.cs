using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

    internal static void RequireName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The value cannot be null, empty or whitespace.", parameterName);
    }
}
