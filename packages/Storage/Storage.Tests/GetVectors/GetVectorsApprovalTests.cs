using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.GetVectors;

namespace Storage.Tests.GetVectors;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).Index(...).GetVectorsAsync</c> puts on the wire; the approved bodies must
/// match the service's <c>GetVectors</c> schema (<c>vectorBucketName</c>, <c>indexName</c>, <c>keys</c>, and
/// <c>returnData</c>/<c>returnMetadata</c> only when asked for, so the service owns their default).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetVectorsApprovalTests : RequestApprovalFixture
{
    private static readonly string[] Keys = { "doc-1", "doc-2" };

    [TestMethod]
    public async Task GetVectorsRequest_ShouldOmitTheContentFlags_GivenNoContent()
    {
        this.RespondWith("{\"vectors\":[]}");
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").GetVectorsAsync(Keys);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task GetVectorsRequest_ShouldAskForDataAndMetadata_GivenBoth()
    {
        this.RespondWith("{\"vectors\":[]}");
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").GetVectorsAsync(Keys, VectorContent.Data | VectorContent.Metadata);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task GetVectorsRequest_ShouldAskForDataOnly_GivenData()
    {
        this.RespondWith("{\"vectors\":[]}");
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").GetVectorsAsync(Keys, VectorContent.Data);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task GetVectorsRequest_ShouldAskForMetadataOnly_GivenMetadata()
    {
        this.RespondWith("{\"vectors\":[]}");
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").GetVectorsAsync(Keys, VectorContent.Metadata);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
