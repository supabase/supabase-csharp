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
using Supabase.Gotrue.UpdateCustomProvider;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.UpdateCustomProvider;

/// <summary>
///     Pins the wire contract of updating a custom OAuth/OIDC provider against a stubbed auth server: the route, the
///     service-role credentials, the response and failure mapping, argument checks, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateCustomProviderContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly CustomProviderChanges Rename = new() { Name = "Renamed" };

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldPutChangesByIdentifierWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.OK, CustomProviderResponses.OAuth2);
        await this.Admin.UpdateCustomProviderAsync("custom:customauth", Rename);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Put)
            .WithPath("/admin/custom-providers/custom:customauth")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithJsonContentType();
    }

    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldReturnUpdatedProvider()
    {
        this.AnswerWith(HttpStatusCode.OK, CustomProviderResponses.Oidc);
        (await this.Admin.UpdateCustomProviderAsync("custom:mycompany", new CustomProviderChanges { Name = "My Company SSO" })).Should().BeEquivalentTo(new
        {
            Id = Guid.Parse("1f0c6b8e-3d1a-4c55-9a43-5f1a2b7c9d10"),
            Type = CustomProviderType.Oidc,
            Identifier = "custom:mycompany",
            Name = "My Company SSO",
        }, "the auth server answers 200 with the provider as stored after the update");
    }

    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldThrowCustomProviderNotFound_GivenUnknownIdentifier()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"custom_provider_not_found","msg":"Custom OAuth provider not found"}""");
        var update = () => this.Admin.UpdateCustomProviderAsync("custom:unknown", Rename);
        await update.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.CustomProviderNotFound,
                "the auth server answers 404 custom_provider_not_found when no provider has the identifier");
    }

    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, "null");
        var update = () => this.Admin.UpdateCustomProviderAsync("custom:mycompany", Rename);
        await update.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no custom provider*",
            "a 200 always carries the updated provider; anything else is a broken response, not a provider");
    }

    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldThrowArgumentNull_GivenNullChanges()
    {
        var update = () => this.Admin.UpdateCustomProviderAsync("custom:mycompany", null!);
        await update.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("changes");
        this.server.CountReceivedRequests().Should().Be(0, "argument misuse is rejected before any I/O");
    }

    [TestMethod]
    public void UpdateCustomProviderAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, CustomProviderResponses.OAuth2);
        BlockingCaller.Completes(() => this.Admin.UpdateCustomProviderAsync("custom:customauth", Rename)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var update = () => this.Admin.UpdateCustomProviderAsync("custom:mycompany", Rename, cancelled.Token);
        await update.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/custom-providers/*")).UsingPut())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
