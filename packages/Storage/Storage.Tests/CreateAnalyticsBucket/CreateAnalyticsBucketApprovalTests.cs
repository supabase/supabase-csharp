using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;

namespace Storage.Tests.CreateAnalyticsBucket;

/// <summary>
/// Pins the exact bytes <c>Analytics.CreateBucketAsync</c> puts on the wire; the approved body must match
/// the service's <c>POST /iceberg/bucket</c> schema (<c>name</c>, required).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateAnalyticsBucketApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task CreateAnalyticsBucketRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith("{\"id\":\"analytics-data\",\"name\":\"analytics-data\",\"created_at\":\"2026-11-24T10:00:00.000Z\",\"updated_at\":\"2026-11-24T10:00:00.000Z\"}");
        await this.Client.Analytics.CreateBucketAsync("analytics-data");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
