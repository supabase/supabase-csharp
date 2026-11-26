using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;

namespace Storage.Tests.VectorBuckets.DeleteVectors;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).Index(...).DeleteVectorsAsync</c> puts on the wire; the approved body must
/// match the service's <c>DeleteVectors</c> schema (<c>vectorBucketName</c>, <c>indexName</c> and <c>keys</c>, camelCase).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteVectorsApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task DeleteVectorsRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("");
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").DeleteVectorsAsync(new[] { "doc-1", "doc-2" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
