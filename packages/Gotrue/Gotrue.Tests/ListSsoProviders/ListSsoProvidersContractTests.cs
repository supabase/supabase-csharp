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
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.ListSsoProviders;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.ListSsoProviders;

/// <summary>
///     Pins the wire contract of listing single sign-on identity providers against a stubbed auth server: the route,
///     the service-role credentials, each filter's query parameter, the response and failure mapping, and
///     cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListSsoProvidersContractTests
{
    private const string ServiceKey = "service-role-jwt";
    private const string NoProviders = """{"items":[]}""";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task ListSsoProvidersAsync_ShouldListEveryProviderWithServiceRoleCredentials_GivenNoFilter()
    {
        this.AnswerWith(HttpStatusCode.OK, NoProviders);
        await this.Admin.ListSsoProvidersAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/admin/sso/providers")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithoutQueryParam("resource_id")
            .WithoutQueryParam("resource_id_prefix");
    }

    [TestMethod]
    [DataRow("acme")]
    [DataRow("a_b%c\\d e&f")]
    public async Task ListSsoProvidersAsync_ShouldFilterByResourceIdAsIs_GivenResourceId(string resourceId)
    {
        this.AnswerWith(HttpStatusCode.OK, NoProviders);
        await this.Admin.ListSsoProvidersAsync(SsoProviderFilter.ByResourceId(resourceId));
        this.server.VerifySingleReceivedRequest()
            .WithQueryParam("resource_id", resourceId)
            .WithoutQueryParam("resource_id_prefix");
    }

    [TestMethod]
    [DataRow("acme-", "acme-")]
    [DataRow("a_b", "a\\_b")]
    [DataRow("a%b", "a\\%b")]
    [DataRow("a\\b", "a\\\\b")]
    [DataRow("\\_%&", "\\\\\\_\\%&")]
    public async Task ListSsoProvidersAsync_ShouldFilterByEscapedPrefix_GivenResourceIdPrefix(string prefix, string wirePrefix)
    {
        this.AnswerWith(HttpStatusCode.OK, NoProviders);
        await this.Admin.ListSsoProvidersAsync(SsoProviderFilter.ByResourceIdPrefix(prefix));
        this.server.VerifySingleReceivedRequest()
            .WithQueryParam("resource_id_prefix", wirePrefix)
            .WithoutQueryParam("resource_id");
    }

    [TestMethod]
    public async Task ListSsoProvidersAsync_ShouldReturnEveryListedProvider()
    {
        this.AnswerWith(HttpStatusCode.OK, $$"""{"items":[{{SsoProviderResponses.Complete}},{{SsoProviderResponses.Sparse}}]}""");
        (await this.Admin.ListSsoProvidersAsync()).Select(provider => (provider.ResourceId, provider.Enabled)).Should().Equal(
            new[] { ("acme", false), ((string?) null, true) },
            "the auth server answers 200 with every provider under items, each mapped like a single provider");
    }

    [TestMethod]
    [DataRow("""{"items":null}""")]
    [DataRow("""{}""")]
    [DataRow("null")]
    public async Task ListSsoProvidersAsync_ShouldReturnEmptyList_GivenNoProvidersInResponse(string body)
    {
        this.AnswerWith(HttpStatusCode.OK, body);
        (await this.Admin.ListSsoProvidersAsync()).Should().BeEmpty("the returned list is never null, whatever the server sends");
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Forbidden, "not_admin")]
    [DataRow(HttpStatusCode.Unauthorized, "no_authorization")]
    [DataRow(HttpStatusCode.Unauthorized, "bad_jwt")]
    public async Task ListSsoProvidersAsync_ShouldThrowAdminTokenRequired_GivenRejectedKey(HttpStatusCode status, string errorCode)
    {
        this.AnswerWith(status, $$"""{"code":{{(int) status}},"error_code":"{{errorCode}}","msg":"rejected"}""");
        var list = () => this.Admin.ListSsoProvidersAsync();
        await list.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server rejects a key that is missing, invalid or without the service_role claim");
    }

    [TestMethod]
    public void ListSsoProvidersAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, NoProviders);
        BlockingCaller.Completes(() => this.Admin.ListSsoProvidersAsync()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task ListSsoProvidersAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var list = () => this.Admin.ListSsoProvidersAsync(cancellationToken: cancelled.Token);
        await list.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath("/admin/sso/providers").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
