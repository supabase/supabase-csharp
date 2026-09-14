using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Gotrue.Tests.Network;

/// <summary>Covers <see cref="NetworkStatus"/>'s HttpClient reuse across ping checks.</summary>
[TestClass]
[TestCategory("Contract")]
public class NetworkStatusContractTests
{
    [TestMethod]
    public async Task PingCheck_ShouldReuseTheInjectedHttpClient_GivenRepeatedCalls()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/ping").UsingGet()).RespondWith(Response.Create().WithStatusCode(200));
        using var injectedClient = new HttpClient();
        injectedClient.DefaultRequestHeaders.Add("X-Injected", "true");
        var status = new NetworkStatus(injectedClient);

        await status.PingCheckAsync($"{server.Url}/ping");
        await status.PingCheckAsync($"{server.Url}/ping");

        server.LogEntries.Should().HaveCount(2, "a fresh HttpClient per call was the bug being fixed");
        server.LogEntries.Should().OnlyContain(e => e.RequestMessage!.Headers!.ContainsKey("X-Injected"),
            "both calls must go through the same injected client, not a freshly constructed one");
    }
}
