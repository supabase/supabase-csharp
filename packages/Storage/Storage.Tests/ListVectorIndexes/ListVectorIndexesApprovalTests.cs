using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.ListVectorIndexes;

namespace Storage.Tests.ListVectorIndexes;

/// <summary>
/// Pins the exact bytes <c>Vectors.From(...).ListIndexesAsync</c> puts on the wire; the approved bodies must match the
/// service's <c>ListIndexes</c> schema (<c>vectorBucketName</c>, then the optional <c>maxResults</c>, <c>nextToken</c>
/// and <c>prefix</c>, camelCase, left out when unset).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListVectorIndexesApprovalTests : RequestApprovalFixture
{
    private const string EmptyPage = "{\"indexes\":[]}";

    [TestMethod]
    public async Task ListIndexesRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith(EmptyPage);
        await this.Client.Vectors.From("embeddings-prod").ListIndexesAsync(new ListVectorIndexesOptions { Prefix = "docs-", MaxResults = 50, NextToken = "docs-v1" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ListIndexesRequest_ShouldOmitUnsetOptions_GivenNoOptions()
    {
        this.RespondWith(EmptyPage);
        await this.Client.Vectors.From("embeddings-prod").ListIndexesAsync();
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
