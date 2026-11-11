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
using Supabase.Gotrue.CreateSsoProvider;
using Supabase.Gotrue.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.CreateSsoProvider;

/// <summary>
///     Pins the wire contract of registering a single sign-on identity provider against a stubbed auth server: the
///     route, the service-role credentials, the response mapping, the error codes each rejection carries, and
///     cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateSsoProviderContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly SamlMetadata MetadataUrl = SamlMetadata.FromUrl(new Uri("https://idp.acme.com/saml/metadata"));

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldPostProviderWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.Created, SsoProviderResponses.Complete);
        await this.Admin.CreateSsoProviderAsync(MetadataUrl);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/admin/sso/providers")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithJsonContentType();
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldReturnRegisteredProvider()
    {
        this.AnswerWith(HttpStatusCode.Created, SsoProviderResponses.Complete);
        (await this.Admin.CreateSsoProviderAsync(MetadataUrl)).Should().BeEquivalentTo(new SsoProvider
        {
            Id = Guid.Parse("0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76"),
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
        }, "the auth server answers 201 with the stored provider, including the metadata it fetched from the URL");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.Created, "null");
        var create = () => this.Admin.CreateSsoProviderAsync(MetadataUrl);
        await create.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no SSO provider*",
            "a 201 always carries the created provider; anything else is a broken response, not a provider");
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest, "validation_failed", "metadata_url is not a HTTPS URL")]
    [DataRow(HttpStatusCode.BadRequest, "saml_metadata_fetch_failed", "HTTP 404 error fetching SAML Metadata from URL 'https://idp.acme.com/saml/metadata'")]
    [DataRow(HttpStatusCode.UnprocessableEntity, "saml_idp_already_exists", "SAML Identity Provider with this EntityID (https://idp.acme.com/saml) already exists")]
    [DataRow(HttpStatusCode.BadRequest, "sso_domain_already_exists", "SSO Domain 'acme.com' is already assigned to an SSO identity provider")]
    [DataRow(HttpStatusCode.InternalServerError, "unexpected_failure", "Unexpected failure, please check server logs for more information")]
    public async Task CreateSsoProviderAsync_ShouldThrowWithErrorCode_GivenRejection(HttpStatusCode status, string errorCode, string message)
    {
        this.AnswerWith(status, $$"""{"code":{{(int) status}},"error_code":"{{errorCode}}","msg":"{{message}}"}""");
        var create = () => this.Admin.CreateSsoProviderAsync(MetadataUrl);
        await create.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == errorCode && failure.StatusCode == (int) status,
                "every rejection of adminSSOProvidersCreate carries its error code through the existing error path");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var create = () => this.Admin.CreateSsoProviderAsync(MetadataUrl);
        await create.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldThrowArgumentNull_GivenNullMetadata()
    {
        var create = () => this.Admin.CreateSsoProviderAsync(null!);
        await create.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("metadata");
    }

    [TestMethod]
    public void CreateSsoProviderAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.Created, SsoProviderResponses.Complete);
        BlockingCaller.Completes(() => this.Admin.CreateSsoProviderAsync(MetadataUrl)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var create = () => this.Admin.CreateSsoProviderAsync(MetadataUrl, cancellationToken: cancelled.Token);
        await create.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldStopWaiting_GivenTokenCancelledWhileServerFetchesMetadata()
    {
        this.server.Given(Request.Create().WithPath("/admin/sso/providers").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Created).WithDelay(TimeSpan.FromSeconds(30))
                .WithHeader("Content-Type", "application/json").WithBody(SsoProviderResponses.Complete));
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var create = () => this.Admin.CreateSsoProviderAsync(MetadataUrl, cancellationToken: cancelled.Token);
        await create.Should().ThrowWithinAsync<OperationCanceledException>(TimeSpan.FromSeconds(10),
            "the token bounds the time the server spends fetching metadata from the URL before it answers");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath("/admin/sso/providers").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
