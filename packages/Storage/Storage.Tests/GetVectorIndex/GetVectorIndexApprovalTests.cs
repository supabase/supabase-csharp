using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;

namespace Storage.Tests.GetVectorIndex;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).GetIndexAsync</c> puts on the wire; the approved body must
/// match the service's <c>GetIndex</c> schema (<c>vectorBucketName</c> and <c>indexName</c>, camelCase).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetVectorIndexApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task GetIndexRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("{\"index\":{\"indexName\":\"docs-v1\",\"dataType\":\"float32\",\"dimension\":3,\"distanceMetric\":\"cosine\",\"creationTime\":1795590525}}");
        await this.Client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
