using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;

namespace Storage.Tests.UpdateBucketLifecycle;

/// <summary>
/// Pins the exact bytes <c>UpdateBucketLifecycleAsync</c> puts on the wire; the approved body must match the
/// service's lifecycle schema (<c>rules</c>, each with <c>status</c>, an empty <c>filter</c> and <c>noncurrentVersionExpiration</c>).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateBucketLifecycleApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task UpdateBucketLifecycleRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("""{"rules":[]}""");
        await this.Client.UpdateBucketLifecycleAsync("avatars", new BucketLifecycle
        {
            Rules = new[]
            {
                new LifecycleRule
                {
                    Id = "keep-two",
                    Status = LifecycleRuleStatus.Enabled,
                    NoncurrentVersionExpiration = new NoncurrentVersionExpiration { NoncurrentDays = 30, NewerNoncurrentVersions = 2 },
                },
                new LifecycleRule
                {
                    Status = LifecycleRuleStatus.Disabled,
                    NoncurrentVersionExpiration = new NoncurrentVersionExpiration { NoncurrentDays = 7 },
                },
            },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
