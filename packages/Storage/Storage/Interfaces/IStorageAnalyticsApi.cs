using System;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;

namespace Supabase.Storage.Interfaces;

/// <summary>
/// Manages analytics buckets, the Apache Iceberg backed buckets of Supabase Storage. Requires a service-role key.
/// </summary>
public interface IStorageAnalyticsApi
{
    /// <summary>
    /// Deletes an empty analytics bucket.
    /// </summary>
    /// <param name="name">The name of the analytics bucket to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="ArgumentException">The name is null, empty, or cannot be sent as a single path segment.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">The service rejected the request, for example because the bucket does not exist or the caller is not authorized.</exception>
    /// <example>
    /// <code>
    /// var storage = new Client(url, headers);
    /// await storage.Analytics.DeleteBucketAsync("analytics-data");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.analytics.delete_analytics_bucket")]
    Task DeleteBucketAsync(string name, CancellationToken cancellationToken = default);
}
