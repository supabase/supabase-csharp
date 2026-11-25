using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;

namespace Storage.Tests.CreateVectorBucket;

/// <summary>
/// Pins the exact bytes <c>Vectors.CreateVectorBucketAsync</c> puts on the wire; the approved body must
/// match the service's <c>CreateVectorBucket</c> schema (<c>vectorBucketName</c>, camelCase).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateVectorBucketApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task CreateVectorBucketRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("");
        await this.Client.Vectors.CreateVectorBucketAsync("embeddings-prod");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
