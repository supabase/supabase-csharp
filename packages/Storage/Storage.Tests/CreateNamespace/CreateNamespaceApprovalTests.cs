using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.CreateNamespace;

namespace Storage.Tests.CreateNamespace;

/// <summary>
/// Pins the exact bytes <c>CreateNamespaceAsync</c> puts on the wire; the approved bodies must match the service's
/// <c>POST /iceberg/v1/:prefix/namespaces</c> schema (<c>namespace</c>, required; <c>properties</c>, string values).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateNamespaceApprovalTests : RequestApprovalFixture
{
    private const string Created = "{\"namespace\":[\"sales\"],\"properties\":{}}";

    [TestMethod]
    public async Task CreateNamespaceRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith(Created);
        await this.Client.Analytics.From("analytics-data").CreateNamespaceAsync(IcebergNamespace.Of("sales"));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateNamespaceRequest_ShouldSerializeProperties_GivenProperties()
    {
        this.RespondWith(Created);
        await this.Client.Analytics.From("analytics-data").CreateNamespaceAsync(
            IcebergNamespace.Of("sales"),
            new CreateNamespaceOptions { Properties = new Dictionary<string, string> { ["owner"] = "data-team" } });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
