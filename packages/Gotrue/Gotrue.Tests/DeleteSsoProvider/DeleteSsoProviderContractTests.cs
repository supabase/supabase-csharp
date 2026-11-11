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

namespace Gotrue.Tests.DeleteSsoProvider;

/// <summary>
///     Pins the wire contract of deleting a single sign-on identity provider against a stubbed auth server: the route
///     for each way of addressing a provider, the service-role credentials, the failure mapping, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteSsoProviderContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid ProviderId = Guid.Parse("0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76");

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldDeleteProviderByIdWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        await this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ById(ProviderId));
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithPath("/admin/sso/providers/0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    [DataRow("acme", "/admin/sso/providers/resource_acme")]
    [DataRow("eu+$&,:;=@1", "/admin/sso/providers/resource_eu+$&,:;=@1")]
    [DataRow("a b%?#\\é", "/admin/sso/providers/resource_a%20b%25%3F%23%5C%C3%A9")]
    public async Task DeleteSsoProviderAsync_ShouldDeleteProviderByPrefixedResourceId(string resourceId, string expectedPath)
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        await this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ByResourceId(resourceId));
        this.server.VerifySingleReceivedRequest().WithEncodedPath(expectedPath);
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldComplete_GivenEmptyResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, string.Empty);
        var delete = () => this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ById(ProviderId));
        await delete.Should().NotThrowAsync("the deleted provider the auth server echoes back is discarded, so its absence is no failure");
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenUnknownProvider()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"sso_provider_not_found","msg":"SSO Identity Provider not found"}""");
        var delete = () => this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ByResourceId("unknown"));
        await delete.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found when no provider matches the id or resource ID");
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var delete = () => this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ById(ProviderId));
        await delete.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public void DeleteSsoProviderAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        BlockingCaller.Completes(() => this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ById(ProviderId))).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var delete = () => this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ById(ProviderId), cancelled.Token);
        await delete.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/sso/providers/*")).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
