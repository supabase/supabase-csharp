using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Common;
using Supabase.Storage.CreateNamespace;
using Supabase.Storage.CreateTable;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;
using Supabase.Storage.ListNamespaces;

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
        RequireArgument(@namespace, nameof(@namespace));
        try
        {
            using var response = await Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Head, this.ResourceUrl(@namespace.Path), null, this.headers(), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SupabaseStorageException exception) when (IsNotFound(exception))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IcebergNamespaceDetails> CreateNamespaceAsync(IcebergNamespace @namespace, CreateNamespaceOptions? options = null, CancellationToken cancellationToken = default)
    {
        RequireArgument(@namespace, nameof(@namespace));
        var response = await Helpers.MakeRequestAsync<CreateNamespaceResponse>(this.requestClient, this.options, HttpMethod.Post, $"{this.catalogUrl}/namespaces", CreateNamespaceBody.For(@namespace, options), this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToDetails(@namespace);
    }

    /// <inheritdoc />
    public async Task<IcebergNamespacePage> ListNamespacesAsync(ListNamespacesOptions? options = null, CancellationToken cancellationToken = default)
    {
        var url = ListNamespacesQuery.For($"{this.catalogUrl}/namespaces", options);
        var response = await Helpers.MakeRequestAsync<ListNamespacesResponse>(this.requestClient, this.options, HttpMethod.Get, url, null, this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToPage();
    }

    /// <inheritdoc />
    public Task DeleteNamespaceAsync(IcebergNamespace @namespace, CancellationToken cancellationToken = default)
    {
        RequireArgument(@namespace, nameof(@namespace));
        return Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Delete, this.ResourceUrl(@namespace.Path), null, this.headers(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IcebergTableMetadata> CreateTableAsync(IcebergTable table, IcebergSchema schema, CreateTableOptions? options = null, CancellationToken cancellationToken = default)
    {
        RequireArgument(table, nameof(table));
        RequireArgument(schema, nameof(schema));
        var response = await Helpers.MakeRequestAsync<TableMetadataResponse>(this.requestClient, this.options, HttpMethod.Post, this.ResourceUrl($"{table.Namespace.Path}/tables"), CreateTableBody.For(table, schema, options), this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToMetadata();
    }

    /// <inheritdoc />
    public async Task<IcebergTableMetadata> LoadTableAsync(IcebergTable table, CancellationToken cancellationToken = default)
    {
        RequireArgument(table, nameof(table));
        var response = await Helpers.MakeRequestAsync<TableMetadataResponse>(this.requestClient, this.options, HttpMethod.Get, this.ResourceUrl(table.Path), null, this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToMetadata();
    }

    private string ResourceUrl(string path) => $"{this.catalogUrl}/{path}";

    private static bool IsNotFound(SupabaseStorageException exception) => exception.StatusCode == NotFoundStatus;

    private static void RequireArgument(object? argument, string parameterName)
    {
        if (argument is null)
            throw new ArgumentNullException(parameterName);
    }
}
