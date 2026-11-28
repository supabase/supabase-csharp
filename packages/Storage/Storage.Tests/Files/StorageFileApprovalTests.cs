using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.FileBuckets.Common;

namespace Storage.Tests.Files;

/// <summary>
///     Pins the exact bytes the file control-plane calls put on the wire — move, copy, signed-URL creation
///     and bulk remove. Each builds a JSON body the SDK controls (including explicit nulls for absent optional
///     fields such as <c>destinationBucket</c>), which is the transport contract the System.Text.Json
///     migration must preserve.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageFileApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task MoveRequest_ShouldSerializeToExpectedPayload_GivenSourceAndDestination()
    {
        this.RespondWith("{}");
        await this.Client.From("photos").Move("old/cat.png", "new/cat.png");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CopyRequest_ShouldSerializeToExpectedPayload_GivenDestinationBucket()
    {
        this.RespondWith("{}");
        await this.Client.From("photos").Copy("cat.png", "cat.png",
            new DestinationOptions { DestinationBucket = "archive" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateSignedUrlRequest_ShouldSerializeToExpectedPayload_GivenExpiry()
    {
        this.RespondWith("{\"signedURL\":\"/sign/cat.png\"}");
        await this.Client.From("photos").CreateSignedUrl("cat.png", 3600);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateSignedUrlRequest_ShouldNestTransform_GivenTransformOptions()
    {
        this.RespondWith("{\"signedURL\":\"/sign/cat.png\"}");
        await this.Client.From("photos").CreateSignedUrl("cat.png", 3600,
            new TransformOptions { Width = 100, Height = 200 });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateSignedUrlsRequest_ShouldSerializeToExpectedPayload_GivenPaths()
    {
        this.RespondWith("[]");
        await this.Client.From("photos").CreateSignedUrls(new List<string> { "cat.png", "dog.png" }, 3600);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task RemoveRequest_ShouldSerializeToExpectedPayload_GivenPaths()
    {
        this.RespondWith("[]");
        await this.Client.From("photos").Remove(new List<string> { "cat.png", "dog.png" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ListRequest_ShouldSerializeToExpectedPayload_GivenVersionFilters()
    {
        this.RespondWith("[]");
        await this.Client.From("photos").List("folder",
            new SearchOptions { NoncurrentVersions = ListingFilter.Include, DeleteMarkers = ListingFilter.Only });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ListV2Request_ShouldSerializeToExpectedPayload_GivenVersionFilters()
    {
        this.RespondWith("{}");
        await this.Client.From("photos").ListV2Async(
            new SearchV2Options { NoncurrentVersions = ListingFilter.Only, DeleteMarkers = ListingFilter.Exclude });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
