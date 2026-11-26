using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.VectorBuckets.QueryVectors;

namespace Storage.Tests.VectorBuckets.QueryVectors;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).Index(...).QueryVectorsAsync</c> puts on the wire; the approved bodies must
/// match the service's <c>QueryVectors</c> schema (<c>vectorBucketName</c>, <c>indexName</c>, <c>queryVector.float32</c>,
/// <c>topK</c>, and <c>filter</c>, <c>nextToken</c>, <c>returnDistance</c>, <c>returnMetadata</c> only when set, camelCase).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class QueryVectorsApprovalTests : RequestApprovalFixture
{
    private const string EmptyResult = "{\"vectors\":[]}";
    private static readonly float[] Query = { 0.5f, 0.25f };

    [TestMethod]
    public async Task QueryVectorsRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith(EmptyResult);
        var options = new QueryVectorsOptions
        {
            Filter = JsonDocument.Parse("{\"$and\":[{\"year\":{\"$gte\":2024}},{\"lang\":{\"$in\":[\"en\",\"fr\"]}}]}").RootElement,
            NextToken = "token",
            Include = VectorMatchContent.Distance | VectorMatchContent.Metadata,
        };
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").QueryVectorsAsync(Query, 10, options);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task QueryVectorsRequest_ShouldOmitUnsetOptions_GivenNoOptions()
    {
        this.RespondWith(EmptyResult);
        await this.Client.Vectors.From("embeddings-prod").Index("docs-v1").QueryVectorsAsync(Query, 10);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
