using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Common;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;

namespace Supabase.Storage;

internal sealed class IcebergCatalog : IIcebergCatalog
{
    private const int NotFoundStatus = 404;

    private readonly string catalogUrl;
    private readonly ClientOptions options;
    private readonly HttpClient requestClient;
    private readonly Func<Dictionary<string, string>> headers;

    internal IcebergCatalog(string catalogUrl, ClientOptions options, HttpClient requestClient, Func<Dictionary<string, string>> headers)
    {
        this.catalogUrl = catalogUrl;
        this.options = options;
        this.requestClient = requestClient;
        this.headers = headers;
    }

    /// <inheritdoc />
    public async Task<bool> NamespaceExistsAsync(IcebergNamespace @namespace, CancellationToken cancellationToken = default)
    {
        RequireNamespace(@namespace);
        try
        {
            using var response = await Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Head, this.NamespaceUrl(@namespace), null, this.headers(), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SupabaseStorageException exception) when (IsNotFound(exception))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task DeleteNamespaceAsync(IcebergNamespace @namespace, CancellationToken cancellationToken = default)
    {
        RequireNamespace(@namespace);
        return Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Delete, this.NamespaceUrl(@namespace), null, this.headers(), cancellationToken);
    }

    private string NamespaceUrl(IcebergNamespace @namespace) =>
        $"{this.catalogUrl}/namespaces/{IcebergNamespacePath.For(@namespace)}";

    private static bool IsNotFound(SupabaseStorageException exception) => exception.StatusCode == NotFoundStatus;

    private static void RequireNamespace(IcebergNamespace? @namespace)
    {
        if (@namespace is null)
            throw new ArgumentNullException(nameof(@namespace));
    }
}
