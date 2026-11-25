using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.ListVectors;

namespace Storage.Tests.ListVectors;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).Index(...).ListVectorsAsync</c> puts on the wire; the approved bodies must
/// match the service's <c>ListVectors</c> schema, with every unset option left out so the service owns its default.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListVectorsApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task ListVectorsRequest_ShouldSendOnlyTheBucketAndIndex_GivenNoOptions()
    {
        this.RespondWith("{\"vectors\":[]}");
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").ListVectorsAsync();
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ListVectorsRequest_ShouldSendEveryOption_GivenAllOptions()
    {
        this.RespondWith("{\"vectors\":[]}");
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").ListVectorsAsync(new ListVectorsOptions
        {
            MaxResults = 100,
            NextToken = "doc-100",
            Include = VectorContent.Data | VectorContent.Metadata,
            Segment = new VectorSegment(1, 4),
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
