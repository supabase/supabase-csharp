using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Storage.Common;
using Supabase.Storage.ListAnalyticsBuckets;

namespace Supabase.Storage.Interfaces;

/// <summary>
/// Manages analytics buckets, the Apache Iceberg backed buckets of Supabase Storage. Requires a service-role key.
/// </summary>
public interface IStorageAnalyticsApi
{
    /// <summary>
    /// Scopes the following operations to the Iceberg REST Catalog of one analytics bucket. Nothing is sent to the service.
    /// </summary>
    /// <param name="bucketName">The name of the analytics bucket, which the catalog calls its warehouse.</param>
    /// <returns>A client for the catalog operations of that bucket.</returns>
    /// <exception cref="ArgumentException">The name is null, empty, whitespace or a relative path segment.</exception>
    /// <example>
    /// <code>
    /// var catalog = storage.Analytics.From("analytics-data");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.analytics.access_catalog")]
    IIcebergCatalog From(string bucketName);

    /// <summary>
    /// Creates an analytics bucket, a bucket backed by the Apache Iceberg table format.
    /// </summary>
    /// <param name="name">The name of the analytics bucket to create. The service validates it.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The bucket the service created.</returns>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">The service rejected the request, for example because the caller is not authorized or the feature is not enabled.</exception>
    /// <example>
    /// <code>
    /// var storage = new Client(url, headers);
    /// var bucket = await storage.Analytics.CreateBucketAsync("analytics-data");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.analytics.create_analytics_bucket")]
    Task<AnalyticsBucket> CreateBucketAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the analytics buckets of the project.
    /// </summary>
    /// <param name="options">Pagination, sort and search options. Without them the service returns every bucket, sorted by name in ascending order.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The buckets, in the order the service returned them. Empty when there are none.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The sort column is not a defined <see cref="AnalyticsBucketSortColumn"/>.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">The service rejected the request, for example because the pagination is out of range, the caller is not authorized or the feature is not enabled.</exception>
    /// <example>
    /// <code>
    /// var storage = new Client(url, headers);
    /// var buckets = await storage.Analytics.ListBucketsAsync(new ListAnalyticsBucketsOptions
    /// {
    ///     Limit = 10,
    ///     Sort = new AnalyticsBucketSort(AnalyticsBucketSortColumn.CreatedAt, ListSortDirection.Descending),
    /// });
    /// </code>
    /// </example>
    [ImplementedCapability("storage.analytics.list_analytics_buckets")]
    Task<IReadOnlyList<AnalyticsBucket>> ListBucketsAsync(ListAnalyticsBucketsOptions? options = null, CancellationToken cancellationToken = default);

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
