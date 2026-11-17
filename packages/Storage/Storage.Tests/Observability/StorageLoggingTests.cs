using System.Threading.Tasks;
using FluentAssertions;
using Storage.Tests.Support;
using Supabase.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.Observability;

/// <summary>
/// Every Storage request logs its outcome and timing at Debug (client.observability.diagnostic_logging).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageLoggingTests
{
    private WireMockServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task ListBuckets_ShouldLogTheRequestOutcomeAtDebug()
    {
        var capture = new LogCapture();
        await new Client($"{this.server.Url}/storage/v1", new ClientOptions { Logger = capture.Logger }, new System.Collections.Generic.Dictionary<string, string>()).ListBuckets();
        var record = capture.Records.Should().ContainSingle().Which;
        record.Message.Should().StartWith($"GET {this.server.Url}/storage/v1/bucket responded 200 in ").And.NotContain("secret", "credentials are never logged");
    }
}
