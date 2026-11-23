using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Common;
using Supabase.Storage.DeleteVectorIndex;
using Supabase.Storage.Interfaces;

namespace Supabase.Storage;

internal sealed class VectorBucket : IVectorBucket
{
    private readonly string vectorBucketName;
    private readonly VectorService service;

    internal VectorBucket(string vectorBucketName, VectorService service)
    {
        this.vectorBucketName = vectorBucketName;
        this.service = service;
    }

    /// <inheritdoc />
    public Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        VectorService.RequireName(indexName, nameof(indexName));
        return this.service.Post("DeleteIndex", new DeleteVectorIndexBody(this.vectorBucketName, indexName), cancellationToken);
    }
}
