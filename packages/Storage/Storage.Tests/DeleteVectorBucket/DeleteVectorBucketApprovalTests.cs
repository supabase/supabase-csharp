using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;

namespace Storage.Tests.DeleteVectorBucket;

/// <summary>
/// Pins the exact bytes <c>Vectors.DeleteVectorBucketAsync</c> puts on the wire; the approved body must
/// match the service's <c>DeleteVectorBucket</c> schema (<c>vectorBucketName</c>, camelCase).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteVectorBucketApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task DeleteVectorBucketRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("");
        await this.Client.Vectors.DeleteVectorBucketAsync("embeddings-prod");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
