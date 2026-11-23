using Supabase.Core.Interfaces;

namespace Supabase.Storage.Interfaces;

public interface IStorageClient<TBucket, TFileObject> : IStorageBucketApi<TBucket>, IGettableHeaders
    where TBucket : Bucket
    where TFileObject : FileObject
{
    IStorageFileApi<TFileObject> From(string id);

    /// <summary>
    /// Operations on vector buckets.
    /// </summary>
    IStorageVectorBucketApi Vectors { get; }
}
