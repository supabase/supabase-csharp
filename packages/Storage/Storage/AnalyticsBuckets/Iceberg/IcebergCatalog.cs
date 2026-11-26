using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;
using Supabase.Storage.AnalyticsBuckets.Iceberg.CreateNamespace;
using Supabase.Storage.AnalyticsBuckets.Iceberg.CreateTable;
using Supabase.Storage.AnalyticsBuckets.Iceberg.DeleteTable;
using Supabase.Storage.AnalyticsBuckets.Iceberg.ListNamespaces;
using Supabase.Storage.AnalyticsBuckets.Iceberg.ListTables;
using Supabase.Storage.AnalyticsBuckets.Iceberg.UpdateTable;
using Supabase.Storage.Common;
using Supabase.Storage.Exceptions;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg;

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
    public Task<bool> NamespaceExistsAsync(IcebergNamespace @namespace, CancellationToken cancellationToken = default)
    {
        RequireArgument(@namespace, nameof(@namespace));
        return this.ResourceExistsAsync(this.ResourceUrl(@namespace.Path), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IcebergNamespaceDetails> LoadNamespaceAsync(IcebergNamespace @namespace, CancellationToken cancellationToken = default)
    {
        RequireArgument(@namespace, nameof(@namespace));
        var response = await Helpers.MakeRequestAsync<NamespaceResponse>(this.requestClient, this.options, HttpMethod.Get, this.ResourceUrl(@namespace.Path), null, this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToDetails(@namespace);
    }

    /// <inheritdoc />
    public async Task<IcebergNamespaceDetails> CreateNamespaceAsync(IcebergNamespace @namespace, CreateNamespaceOptions? options = null, CancellationToken cancellationToken = default)
    {
        RequireArgument(@namespace, nameof(@namespace));
        var response = await Helpers.MakeRequestAsync<NamespaceResponse>(this.requestClient, this.options, HttpMethod.Post, $"{this.catalogUrl}/namespaces", CreateNamespaceBody.For(@namespace, options), this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToDetails(@namespace);
    }

    /// <inheritdoc />
    public async Task<Page<IcebergNamespace>> ListNamespacesAsync(ListNamespacesOptions? options = null, CancellationToken cancellationToken = default)
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
        var response = await Helpers.MakeRequestAsync<TableMetadataResponse>(this.requestClient, this.options, HttpMethod.Post, this.ResourceUrl(table.CollectionPath), CreateTableBody.For(table, schema, options), this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToMetadata();
    }

    /// <inheritdoc />
    public Task<bool> TableExistsAsync(IcebergTable table, CancellationToken cancellationToken = default)
    {
        RequireArgument(table, nameof(table));
        return this.ResourceExistsAsync(this.ResourceUrl(table.Path), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IcebergTableMetadata> LoadTableAsync(IcebergTable table, CancellationToken cancellationToken = default)
    {
        RequireArgument(table, nameof(table));
        var response = await Helpers.MakeRequestAsync<TableMetadataResponse>(this.requestClient, this.options, HttpMethod.Get, this.ResourceUrl(table.Path), null, this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToMetadata();
    }

    /// <inheritdoc />
    public Task DeleteTableAsync(IcebergTable table, DeleteTableOptions? options = null, CancellationToken cancellationToken = default)
    {
        RequireArgument(table, nameof(table));
        return Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Delete, DeleteTableQuery.For(this.ResourceUrl(table.Path), options), null, this.headers(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Page<IcebergTable>> ListTablesAsync(IcebergNamespace @namespace, ListTablesOptions? options = null, CancellationToken cancellationToken = default)
    {
        RequireArgument(@namespace, nameof(@namespace));
        var url = ListTablesQuery.For(this.ResourceUrl(@namespace.TablesPath), options);
        var response = await Helpers.MakeRequestAsync<ListTablesResponse>(this.requestClient, this.options, HttpMethod.Get, url, null, this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToPage(@namespace);
    }

    /// <inheritdoc />
    public async Task<IcebergTableMetadata> UpdateTableAsync(IcebergTable table, IcebergTableChanges changes, CancellationToken cancellationToken = default)
    {
        RequireArgument(table, nameof(table));
        RequireArgument(changes, nameof(changes));
        changes.HasChangesOrThrow(nameof(changes));
        var response = await Helpers.MakeRequestAsync<TableMetadataResponse>(this.requestClient, this.options, HttpMethod.Post, this.ResourceUrl(table.Path), UpdateTableBody.For(changes), this.headers(), cancellationToken).ConfigureAwait(false);
        return response!.ToMetadata();
    }

    private string ResourceUrl(string path) => $"{this.catalogUrl}/{path}";

    private async Task<bool> ResourceExistsAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Head, url, null, this.headers(), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SupabaseStorageException exception) when (IsNotFound(exception))
        {
            return false;
        }
    }

    private static bool IsNotFound(SupabaseStorageException exception) => exception.StatusCode == NotFoundStatus;

    private static void RequireArgument(object? argument, string parameterName)
    {
        if (argument is null)
            throw new ArgumentNullException(parameterName);
    }
}
