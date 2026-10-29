#region

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Common;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.ListCustomProviders;

/// <summary>
///     Pins the wire contract of listing custom OAuth/OIDC providers against a stubbed auth server: the route, the
///     service-role credentials, the type filter, the response mapping, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListCustomProvidersContractTests
{
    private const string ServiceKey = "service-role-jwt";
    private const string NoProviders = """{"providers":[]}""";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task ListCustomProvidersAsync_ShouldListEveryProviderWithServiceRoleCredentials_GivenNoType()
    {
        this.AnswerWith(NoProviders);
        await this.Admin.ListCustomProvidersAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/admin/custom-providers")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithoutQueryParam("type");
    }

    [TestMethod]
    [DataRow(CustomProviderType.OAuth2, "oauth2")]
    [DataRow(CustomProviderType.Oidc, "oidc")]
    public async Task ListCustomProvidersAsync_ShouldFilterByType_GivenType(CustomProviderType type, string wireType)
    {
        this.AnswerWith(NoProviders);
        await this.Admin.ListCustomProvidersAsync(type);
        this.server.VerifySingleReceivedRequest().WithQueryParam("type", wireType);
    }

    [TestMethod]
    public async Task ListCustomProvidersAsync_ShouldReturnEveryListedProvider()
    {
        this.AnswerWith($$"""{"providers":[{{CustomProviderResponses.OAuth2}},{{CustomProviderResponses.Oidc}}]}""");
        (await this.Admin.ListCustomProvidersAsync()).Select(provider => (provider.Identifier, provider.Type)).Should().Equal(
            new[] { ("custom:customauth", CustomProviderType.OAuth2), ("custom:mycompany", CustomProviderType.Oidc) },
            "the auth server answers 200 with every stored provider under providers");
    }

    [TestMethod]
    [DataRow("""{"providers":null}""")]
    [DataRow("""{}""")]
    [DataRow("null")]
    public async Task ListCustomProvidersAsync_ShouldReturnEmptyList_GivenNoProvidersInResponse(string body)
    {
        this.AnswerWith(body);
        (await this.Admin.ListCustomProvidersAsync()).Should().BeEmpty("the returned list is never null, whatever the server sends");
    }

    [TestMethod]
    public void ListCustomProvidersAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(NoProviders);
        BlockingCaller.Completes(() => this.Admin.ListCustomProvidersAsync()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task ListCustomProvidersAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var list = () => this.Admin.ListCustomProvidersAsync(cancellationToken: cancelled.Token);
        await list.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(string body) =>
        this.server.Given(Request.Create().WithPath("/admin/custom-providers").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
