using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.FileBuckets.Common;

namespace Storage.Tests.Buckets;

/// <summary>
///     Pins the exact bytes the bucket create/update calls put on the wire. Both serialize a whole
///     <see cref="Bucket" />, so this captures its per-property null handling (<c>file_size_limit</c> is
///     emitted as null, <c>allowed_mime_types</c> is omitted) and the null owner/timestamp fields — the
///     serialization surface the System.Text.Json migration must preserve.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageBucketApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task CreateBucketRequest_ShouldSerializeToExpectedPayload_GivenDefaults()
    {
        this.RespondWith("{}");
        await this.Client.CreateBucket("photos");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateBucketRequest_ShouldSerializeAllOptions_GivenPublicSizeAndMimeTypes()
    {
        this.RespondWith("{}");
        await this.Client.CreateBucket("photos", new BucketUpsertOptions
        {
            Public = true,
            FileSizeLimit = "50mb",
            AllowedMimes = new List<string> { "image/png", "image/jpeg" }
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateBucketRequest_ShouldSerializeToExpectedPayload_GivenOptions()
    {
        this.RespondWith("{}");
        await this.Client.UpdateBucket("photos", new BucketUpsertOptions { Public = true, FileSizeLimit = "1mb" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateBucketRequest_ShouldSerializeTheVersioningStatus_GivenOptions()
    {
        this.RespondWith("{}");
        await this.Client.CreateBucket("photos", new BucketUpsertOptions { VersioningStatus = BucketVersioningStatus.Enabled });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateBucketRequest_ShouldSerializeTheVersioningStatus_GivenOptions()
    {
        this.RespondWith("{}");
        await this.Client.UpdateBucket("photos", new BucketUpsertOptions { Public = true, VersioningStatus = BucketVersioningStatus.Suspended });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
