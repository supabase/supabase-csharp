#region

using System;
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
using Supabase.Gotrue.Exceptions;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.GetCustomProvider;

/// <summary>
///     Pins the wire contract of fetching a custom OAuth/OIDC provider against a stubbed auth server: the route, the
///     service-role credentials, the response and failure mapping, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetCustomProviderContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    [DataRow("custom:mycompany")]
    [DataRow("custom:my.company")]
    public async Task GetCustomProviderAsync_ShouldFetchProviderByIdentifierWithServiceRoleCredentials(string identifier)
    {
        this.AnswerWith(HttpStatusCode.OK, CustomProviderResponses.Oidc);
        await this.Admin.GetCustomProviderAsync(identifier);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath($"/admin/custom-providers/{identifier}")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    public async Task GetCustomProviderAsync_ShouldReturnProvider()
    {
        this.AnswerWith(HttpStatusCode.OK, CustomProviderResponses.OAuth2);
        (await this.Admin.GetCustomProviderAsync("custom:customauth")).Should().BeEquivalentTo(new
        {
            Id = Guid.Parse("6ea7e0b6-ee41-4791-802c-ea0625ee8d6b"),
            Type = CustomProviderType.OAuth2,
            Identifier = "custom:customauth",
            AuthorizationUrl = new Uri("https://provider.example.com/oauth/authorize"),
        }, "the auth server answers 200 with the stored provider, as in its CustomOAuthProviderSchema");
    }

    [TestMethod]
    public async Task GetCustomProviderAsync_ShouldThrowCustomProviderNotFound_GivenUnknownIdentifier()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"custom_provider_not_found","msg":"Custom OAuth provider not found"}""");
        var get = () => this.Admin.GetCustomProviderAsync("custom:unknown");
        await get.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.CustomProviderNotFound,
                "the auth server answers 404 custom_provider_not_found when no provider has the identifier");
    }

    [TestMethod]
    public async Task GetCustomProviderAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, "null");
        var get = () => this.Admin.GetCustomProviderAsync("custom:mycompany");
        await get.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no custom provider*",
            "a 200 always carries the provider; anything else is a broken response, not a provider");
    }

    [TestMethod]
    public void GetCustomProviderAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, CustomProviderResponses.OAuth2);
        BlockingCaller.Completes(() => this.Admin.GetCustomProviderAsync("custom:customauth")).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task GetCustomProviderAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var get = () => this.Admin.GetCustomProviderAsync("custom:mycompany", cancelled.Token);
        await get.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/custom-providers/*")).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
