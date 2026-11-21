namespace Supabase.Storage
{
    /// <summary>
    /// Options for <see cref="Interfaces.IStorageFileApi{TFileObject}.PurgeCache"/>,
    /// <see cref="Interfaces.IStorageBucketApi{TBucket}.PurgeBucketCache"/> and
    /// <see cref="Interfaces.IStorageBucketApi{TBucket}.PurgeProjectCacheAsync"/>.
    /// </summary>
    public class PurgeCacheOptions
    {
        /// <summary>
        /// If <c>true</c>, purges only the transformations (resized/formatted variants) for the object,
        /// bucket or project, leaving the original cached file intact. If left <c>null</c>, all cached
        /// versions are purged.
        /// </summary>
        public bool? Transformations { get; set; }
    }
}
