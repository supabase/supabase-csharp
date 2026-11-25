using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.ListVectorBuckets;

namespace Storage.Tests.ListVectorBuckets;

/// <summary>
/// Pins the exact bytes <c>Vectors.ListVectorBucketsAsync</c> puts on the wire; the approved bodies must match the
/// service's <c>ListVectorBuckets</c> schema (the optional <c>maxResults</c>, <c>nextToken</c>
/// and <c>prefix</c>, camelCase, left out when unset).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListVectorBucketsApprovalTests : RequestApprovalFixture
{
    private const string EmptyPage = "{\"vectorBuckets\":[]}";

    [TestMethod]
    public async Task ListVectorBucketsRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith(EmptyPage);
        await this.Client.Vectors.ListVectorBucketsAsync(new ListVectorBucketsOptions { Prefix = "embeddings-", MaxResults = 50, NextToken = "embeddings-v1" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ListVectorBucketsRequest_ShouldOmitUnsetOptions_GivenNoOptions()
    {
        this.RespondWith(EmptyPage);
        await this.Client.Vectors.ListVectorBucketsAsync();
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
