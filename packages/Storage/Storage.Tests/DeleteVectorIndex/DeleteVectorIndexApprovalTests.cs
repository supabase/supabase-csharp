using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;

namespace Storage.Tests.DeleteVectorIndex;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).DeleteIndexAsync</c> puts on the wire; the approved body must
/// match the service's <c>DeleteIndex</c> schema (<c>vectorBucketName</c> and <c>indexName</c>, camelCase).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteVectorIndexApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task DeleteIndexRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("");
        await this.Client.Vectors.From("embeddings-prod").DeleteIndexAsync("docs-v1");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
