using System.Net.Http;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Gotrue.Tests.Observability;

/// <summary>
/// Every Auth request identifies the SDK in a single <c>X-Client-Info</c> header, unless the application supplied its
/// own value through the client's headers, in any casing (client.observability.client_info).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GotrueClientInfoTests
{
    private const string SuppliedValue = "my-lib/1.0";

    private static readonly string SdkValue = Util.GetAssemblyVersion(typeof(Client));

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task Settings_ShouldSendTheSdkValue()
    {
        await new Client(this.Options()).Settings();
        this.server.VerifySingleReceivedRequest().WithHeader("X-Client-Info", SdkValue);
    }

    [TestMethod]
    public async Task Settings_ShouldSendTheSuppliedValue_GivenClientHeaderInAnyCasing()
    {
        var options = this.Options();
        options.Headers["x-client-info"] = SuppliedValue;
        await new Client(options).Settings();
        this.server.VerifySingleReceivedRequest().WithHeader("X-Client-Info", SuppliedValue);
    }

    [TestMethod]
    public async Task Settings_ShouldSendTheSuppliedValue_GivenDynamicHeaders()
    {
        await new Client(this.Options()) { GetHeaders = () => new() { ["X-Client-Info"] = SuppliedValue } }.Settings();
        this.server.VerifySingleReceivedRequest().WithHeader("X-Client-Info", SuppliedValue);
    }

    [TestMethod]
    public async Task AdminRequest_ShouldSendTheSdkValue()
    {
        await new AdminClient("service-key", this.Options()).ListUsers();
        this.server.VerifySingleReceivedRequest().WithHeader("X-Client-Info", SdkValue);
    }

    [TestMethod]
    public async Task StatelessRequest_ShouldSendTheSdkValue()
    {
        await new StatelessClient().Settings(new StatelessClient.StatelessClientOptions { Url = this.server.Url });
        this.server.VerifySingleReceivedRequest().WithHeader("X-Client-Info", SdkValue);
    }

    [TestMethod]
    public async Task PingCheck_ShouldSendTheSdkValue()
    {
        using var httpClient = new HttpClient();
        await new NetworkStatus(httpClient).PingCheckAsync($"{this.server.Url}/settings");
        this.server.VerifySingleReceivedRequest().WithHeader("X-Client-Info", SdkValue);
    }

    private ClientOptions Options() => new() { Url = this.server.Url };
}
