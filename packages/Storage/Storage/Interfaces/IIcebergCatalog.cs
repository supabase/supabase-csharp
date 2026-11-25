using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Storage.Common;
using Supabase.Storage.CreateNamespace;
using Supabase.Storage.ListNamespaces;

namespace Supabase.Storage.Interfaces;

/// <summary>
/// Operations on the Iceberg REST Catalog of one analytics bucket, obtained from <see cref="IStorageAnalyticsApi.From"/>.
/// Requires a service-role key.
/// </summary>
public interface IIcebergCatalog
{
    /// <summary>
    /// Checks whether a namespace exists, without loading its properties.
    /// </summary>
    /// <param name="namespace">The namespace to look for.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>
    /// True when the namespace exists. False when the service answers 404, which a missing namespace, a missing
    /// analytics bucket and a service without Iceberg enabled all do: a HEAD response carries no body to tell them apart.
    /// </returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="namespace"/> is null.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">The service refused the call for another reason, for example because the caller is not authorized.</exception>
    /// <example>
    /// <code>
    /// var catalog = storage.Analytics.From("analytics-data");
    /// var exists = await catalog.NamespaceExistsAsync(IcebergNamespace.Of("sales"));
    /// </code>
    /// </example>
    [ImplementedCapability("storage.analytics.namespace_exists")]
    Task<bool> NamespaceExistsAsync(IcebergNamespace @namespace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a namespace. Creating a namespace that already exists succeeds: Storage merges the given properties into
    /// the stored ones, where the Iceberg REST spec answers 409.
    /// </summary>
    /// <param name="namespace">The namespace to create. The service validates its name and only accepts a single level today.</param>
    /// <param name="options">Optional properties to store against the namespace.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The namespace as the service stored it, with all its properties after the merge.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="namespace"/> is null.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the name is invalid (status 400, <c>Code</c> <c>InvalidParameter</c>),
    /// the bucket does not exist (status 404, <c>Code</c> <c>NoSuchCatalog</c>), the namespace limit is reached
    /// (status 409, <c>Code</c> <c>IcebergMaximumResourceLimit</c>) or the caller is not authorized.
    /// </exception>
    /// <example>
    /// <code>
    /// var catalog = storage.Analytics.From("analytics-data");
    /// var created = await catalog.CreateNamespaceAsync(
    ///     IcebergNamespace.Of("sales"),
    ///     new CreateNamespaceOptions { Properties = new Dictionary&lt;string, string&gt; { ["owner"] = "data-team" } });
    /// </code>
    /// </example>
    [ImplementedCapability("storage.analytics.create_namespace")]
    Task<IcebergNamespaceDetails> CreateNamespaceAsync(IcebergNamespace @namespace, CreateNamespaceOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the namespaces of the analytics bucket, one page at a time. Storage ignores the options today and returns
    /// every namespace in one page, without a next-page token.
    /// </summary>
    /// <param name="options">Parent, page size and page token, following the Iceberg REST Catalog spec. When null, none is sent.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>One page of namespaces. Empty when there are none.</returns>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket does not exist (status 404, <c>Code</c>
    /// <c>NoSuchCatalog</c>) or the caller is not authorized.
    /// </exception>
    /// <example>
    /// <code>
    /// var catalog = storage.Analytics.From("analytics-data");
    /// var page = await catalog.ListNamespacesAsync();
    /// foreach (var @namespace in page.Namespaces)
    ///     Console.WriteLine(@namespace);
    /// </code>
    /// </example>
    [ImplementedCapability("storage.analytics.list_namespaces")]
    Task<IcebergNamespacePage> ListNamespacesAsync(ListNamespacesOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an empty namespace.
    /// </summary>
    /// <param name="namespace">The namespace to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="namespace"/> is null.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the namespace or its bucket does not exist (status 404,
    /// <c>Code</c> <c>NoSuchNamespaceException</c> or <c>NoSuchCatalog</c>), the namespace still holds tables (status 400,
    /// <c>Code</c> <c>IcebergResourceNotEmpty</c>) or the caller is not authorized.
    /// </exception>
    /// <example>
    /// <code>
    /// var catalog = storage.Analytics.From("analytics-data");
    /// await catalog.DeleteNamespaceAsync(IcebergNamespace.Of("sales"));
    /// </code>
    /// </example>
    [ImplementedCapability("storage.analytics.delete_namespace")]
    Task DeleteNamespaceAsync(IcebergNamespace @namespace, CancellationToken cancellationToken = default);
}
