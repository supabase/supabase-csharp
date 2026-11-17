using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Observability;

/// <summary>
/// Every Database request logs its outcome and timing at Debug (client.observability.diagnostic_logging).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class PostgrestLoggingTests
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
    public async Task Get_ShouldLogTheRequestOutcomeAtDebug()
    {
        var capture = new LogCapture();
        await new Client(this.server.Url!, new ClientOptions { Logger = capture.Logger }).Table<Todo>().Get();
        var record = capture.Records.Should().ContainSingle().Which;
        record.Message.Should().StartWith($"GET {this.server.Url}/todos responded 200 in ").And.NotContain("secret", "credentials are never logged");
    }
}
