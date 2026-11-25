using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Storage.NamespaceExists;

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
}
