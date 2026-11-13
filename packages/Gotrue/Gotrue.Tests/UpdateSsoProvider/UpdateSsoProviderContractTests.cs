#region

using System;
using System.Collections.Generic;
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
using Supabase.Gotrue.UpdateSsoProvider;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.UpdateSsoProvider;

/// <summary>
///     Pins the wire contract of changing a single sign-on identity provider against a stubbed auth server: the route
///     for each way of addressing a provider, the service-role credentials, the response mapping, the failures each
///     rejection maps to, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateSsoProviderContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid ProviderId = Guid.Parse("0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76");

    private static readonly SsoProviderChanges Changes = SsoProviderChanges.WithoutNameIdFormat() with { Enabled = false };

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldPutChangesByIdWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        await this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(ProviderId), Changes);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Put)
            .WithPath("/admin/sso/providers/0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithJsonContentType();
    }

    [TestMethod]
    [DataRow("acme", "/admin/sso/providers/resource_acme")]
    [DataRow("eu+$&,:;=@1", "/admin/sso/providers/resource_eu+$&,:;=@1")]
    [DataRow("a b%?#\\é", "/admin/sso/providers/resource_a%20b%25%3F%23%5C%C3%A9")]
    public async Task UpdateSsoProviderAsync_ShouldPutChangesByPrefixedResourceId(string resourceId, string expectedPath)
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        await this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ByResourceId(resourceId), Changes);
        this.server.VerifySingleReceivedRequest().WithEncodedPath(expectedPath);
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldReturnUpdatedProvider()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        (await this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(ProviderId), Changes)).Should().BeEquivalentTo(new SsoProvider
        {
            Id = ProviderId,
            ResourceId = "acme",
            Enabled = false,
            Domains = new[] { "acme.com", "acme.org" },
            CreatedAt = new DateTimeOffset(2026, 11, 11, 10, 25, 38, TimeSpan.Zero).AddTicks(8093850),
            UpdatedAt = new DateTimeOffset(2026, 11, 11, 10, 26, 3, TimeSpan.FromHours(1)).AddTicks(9791030),
            Saml = new SamlConfiguration
            {
                EntityId = "https://idp.acme.com/saml",
                MetadataXml = """<md:EntityDescriptor xmlns:md="urn:oasis:names:tc:SAML:2.0:metadata" entityID="https://idp.acme.com/saml"/>""",
                MetadataUrl = new Uri("https://idp.acme.com/saml/metadata"),
                NameIdFormat = SamlNameIdFormat.EmailAddress,
                AttributeMapping = new Dictionary<string, SamlAttribute>
                {
                    ["email"] = new() { Names = new[] { "mail", "email", "emailaddress" } },
                    ["groups"] = new() { Names = new[] { "memberOf" }, KeepsAllValues = true },
                    ["role"] = new() { Default = "member" },
                },
            },
        }, "the auth server answers 200 with the provider as stored after the change");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, "null");
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(ProviderId), Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no SSO provider*",
            "a 200 always carries the updated provider; anything else is a broken response, not a provider");
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest, "saml_entity_id_mismatch", "SAML Metadata can be updated only if the EntityID matches for the provider")]
    [DataRow(HttpStatusCode.BadRequest, "sso_domain_already_exists", "SSO domain 'acme.com' already assigned to another provider")]
    [DataRow(HttpStatusCode.BadRequest, "validation_failed", "metadata_url is not a HTTPS URL")]
    [DataRow(HttpStatusCode.BadRequest, "saml_metadata_fetch_failed", "HTTP 404 error fetching SAML Metadata from URL 'https://idp.acme.com/saml/metadata'")]
    [DataRow(HttpStatusCode.UnprocessableEntity, "conflict", "Updating SSO provider failed, likely due to a conflict. Try again?")]
    [DataRow(HttpStatusCode.InternalServerError, "unexpected_failure", "Unexpected failure, please check server logs for more information")]
    public async Task UpdateSsoProviderAsync_ShouldThrowWithErrorCode_GivenRejection(HttpStatusCode status, string errorCode, string message)
    {
        this.AnswerWith(status, $$"""{"code":{{(int) status}},"error_code":"{{errorCode}}","msg":"{{message}}"}""");
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(ProviderId), Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == errorCode && failure.StatusCode == (int) status,
                "every rejection of adminSSOProvidersUpdate carries its error code through the existing error path");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenUnknownProvider()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"sso_provider_not_found","msg":"SSO Identity Provider not found"}""");
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ByResourceId("unknown"), Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found when no provider matches the id or resource ID");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(ProviderId), Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public void UpdateSsoProviderAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        BlockingCaller.Completes(() => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(ProviderId), Changes)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(ProviderId), Changes, cancelled.Token);
        await update.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldStopWaiting_GivenTokenCancelledWhileServerFetchesMetadata()
    {
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/sso/providers/*")).UsingPut())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithDelay(TimeSpan.FromSeconds(30))
                .WithHeader("Content-Type", "application/json").WithBody(SsoProviderResponses.Complete));
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(ProviderId),
            SsoProviderChanges.WithoutNameIdFormat() with { Metadata = SamlMetadata.FromUrl(new Uri("https://idp.acme.com/saml/metadata")) },
            cancelled.Token);
        await update.Should().ThrowWithinAsync<OperationCanceledException>(TimeSpan.FromSeconds(10),
            "the token bounds the time the server spends fetching new metadata from the URL before it answers");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/sso/providers/*")).UsingPut())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
