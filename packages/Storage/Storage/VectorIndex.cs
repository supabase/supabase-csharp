using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Common;
using Supabase.Storage.DeleteVectors;
using Supabase.Storage.Interfaces;

namespace Supabase.Storage;

internal sealed class VectorIndex : IVectorIndex
{
    private readonly string vectorBucketName;
    private readonly string indexName;
    private readonly VectorService service;

    internal VectorIndex(string vectorBucketName, string indexName, VectorService service)
    {
        this.vectorBucketName = vectorBucketName;
        this.indexName = indexName;
        this.service = service;
    }

    /// <inheritdoc />
    public Task DeleteVectorsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys is null)
            throw new ArgumentNullException(nameof(keys));
        return this.service.Post("DeleteVectors", new DeleteVectorsBody(this.vectorBucketName, this.indexName, keys), cancellationToken);
    }
}
