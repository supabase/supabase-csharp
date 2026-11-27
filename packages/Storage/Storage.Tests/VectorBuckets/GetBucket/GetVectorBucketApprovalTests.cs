using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;

namespace Storage.Tests.VectorBuckets.GetBucket;

/// <summary>
/// Pins the exact bytes <c>Vectors.GetVectorBucketAsync</c> puts on the wire; the approved body must
/// match the service's <c>GetVectorBucket</c> schema (<c>vectorBucketName</c>, camelCase).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetVectorBucketApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task GetVectorBucketRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("{\"vectorBucket\":{\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":1795590525}}");
        await this.Client.Vectors.GetVectorBucketAsync("embeddings-prod");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
