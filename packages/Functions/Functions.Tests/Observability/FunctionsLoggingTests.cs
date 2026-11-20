using System.Threading.Tasks;
using FluentAssertions;
using Functions.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Functions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Functions.Tests.Observability;

/// <summary>
/// Every function invocation logs its outcome and timing at Debug (client.observability.diagnostic_logging).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class FunctionsLoggingTests
{
    private WireMockServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task Invoke_ShouldLogTheRequestOutcomeAtDebug()
    {
        var capture = new LogCapture();
        await new Client(this.server.Url!, options: new ClientOptions { Logger = capture.Logger }).Invoke("hello");
        var record = capture.Records.Should().ContainSingle().Which;
        record.Message.Should().StartWith($"POST {this.server.Url}/hello responded 200 in ").And.NotContain("secret", "credentials are never logged");
    }
}
