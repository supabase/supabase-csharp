using Supabase.Core.Interfaces;

namespace Supabase.Storage.Interfaces;

public interface IStorageClient<TBucket, TFileObject> : IStorageBucketApi<TBucket>, IGettableHeaders
    where TBucket : Bucket
    where TFileObject : FileObject
{
    IStorageFileApi<TFileObject> From(string id);

    /// <summary>
    /// Manages analytics (Apache Iceberg) buckets. Requires a service-role key.
    /// </summary>
    IStorageAnalyticsApi Analytics { get; }

    /// <summary>
    /// Operations on vector buckets.
    /// </summary>
    IStorageVectorBucketApi Vectors { get; }
}
