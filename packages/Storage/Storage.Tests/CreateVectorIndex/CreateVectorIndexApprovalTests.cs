using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;

namespace Storage.Tests.CreateVectorIndex;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).CreateIndexAsync</c> puts on the wire; the approved bodies must
/// match the service's <c>CreateIndex</c> schema (camelCase, lowercase enum values, nested metadataConfiguration).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateVectorIndexApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task CreateIndexRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("");
        await this.Client.Vectors.From("embeddings-prod").CreateIndexAsync(new NewVectorIndex
        {
            IndexName = "docs-v1",
            Dimension = 1536,
            DistanceMetric = VectorDistanceMetric.Cosine,
            NonFilterableMetadataKeys = new[] { "raw_text" },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateIndexRequest_ShouldOmitMetadataConfiguration_GivenNoNonFilterableKeys()
    {
        this.RespondWith("");
        await this.Client.Vectors.From("embeddings-prod").CreateIndexAsync(new NewVectorIndex
        {
            IndexName = "docs-v1",
            Dimension = 3,
            DistanceMetric = VectorDistanceMetric.Euclidean,
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
