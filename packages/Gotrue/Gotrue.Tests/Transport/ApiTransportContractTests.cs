using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Gotrue.Tests.Transport;

/// <summary>Covers injectable HttpClient and retry-policy wiring through <see cref="Client"/> to the transport layer.</summary>
[TestClass]
[TestCategory("Contract")]
public class ApiTransportContractTests
{
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task RefreshToken_ShouldSendThroughTheInjectedHttpClient_GivenClientOptions()
    {
        using var injectedClient = new HttpClient();
        injectedClient.DefaultRequestHeaders.Add("X-Injected", "true");
        var client = new Client(new ClientOptions { Url = this.server.Url, AllowUnconfirmedUserSessions = true, HttpClient = injectedClient });
        this.MockTokenSuccess();

        await client.RefreshToken("access", "refresh");

        this.server.VerifySingleReceivedRequest().WithHeader("X-Injected", "true");
        client.Shutdown();
    }

    [TestMethod]
    public async Task RefreshToken_ShouldRetryUntilSuccess_GivenRetryableStatus()
    {
        var client = new Client(new ClientOptions
        {
            Url = this.server.Url,
            AllowUnconfirmedUserSessions = true,
            Retry = new RetryOptions { MaxRetries = 1, BaseDelay = TimeSpan.FromMilliseconds(1) },
        });
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .InScenario("retry").WillSetStateTo("retried")
            .RespondWith(Response.Create().WithStatusCode(503));
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .InScenario("retry").WhenStateIs("retried")
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"new-token\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600,\"token_type\":\"bearer\"}"));

        await client.RefreshToken("access", "refresh");

        client.CurrentSession?.AccessToken.Should().Be("new-token", "the retryable 503 should be retried until the second response succeeds");
        client.Shutdown();
    }

    private void MockTokenSuccess() =>
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"new-token\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600,\"token_type\":\"bearer\"}"));
}
