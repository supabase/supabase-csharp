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
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.GetSsoProvider;

/// <summary>
///     Pins the wire contract of fetching a single sign-on identity provider against a stubbed auth server: the route
///     for each way of addressing a provider, the service-role credentials, the response and failure mapping, and
///     cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetSsoProviderContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid ProviderId = Guid.Parse("0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76");

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldFetchProviderByIdWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        await this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId));
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/admin/sso/providers/0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    [DataRow("acme", "/admin/sso/providers/resource_acme")]
    [DataRow("eu+$&,:;=@1", "/admin/sso/providers/resource_eu+$&,:;=@1")]
    [DataRow("a b%?#\\é", "/admin/sso/providers/resource_a%20b%25%3F%23%5C%C3%A9")]
    public async Task GetSsoProviderAsync_ShouldFetchProviderByPrefixedResourceId(string resourceId, string expectedPath)
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        await this.Admin.GetSsoProviderAsync(SsoProviderReference.ByResourceId(resourceId));
        this.server.VerifySingleReceivedRequest().WithEncodedPath(expectedPath);
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldReturnProvider()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        (await this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId))).Should().BeEquivalentTo(new SsoProvider
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
        }, "the auth server answers 200 with the provider; disabled maps to Enabled, domains flatten to their names, and an attribute's name is tried before its names");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldReturnDefaults_GivenSparseProvider()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Sparse);
        (await this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId))).Should().BeEquivalentTo(new
        {
            ResourceId = (string?) null,
            Enabled = true,
            Domains = Array.Empty<string>(),
            Saml = new
            {
                MetadataXml = (string?) null,
                MetadataUrl = (Uri?) null,
                NameIdFormat = (SamlNameIdFormat?) null,
                AttributeMapping = new Dictionary<string, SamlAttribute>(),
            },
        }, "a provider registered with nothing optional has disabled null, domains null, no NameID format and an empty attribute mapping");
    }

    [TestMethod]
    [DataRow("urn:oasis:names:tc:SAML:2.0:nameid-format:persistent", SamlNameIdFormat.Persistent)]
    [DataRow("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress", SamlNameIdFormat.EmailAddress)]
    [DataRow("urn:oasis:names:tc:SAML:2.0:nameid-format:transient", SamlNameIdFormat.Transient)]
    [DataRow("urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified", SamlNameIdFormat.Unspecified)]
    public async Task GetSsoProviderAsync_ShouldReadNameIdFormat_GivenItsUrn(string urn, SamlNameIdFormat expected)
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.WithNameIdFormat(urn));
        (await this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId))).Saml.NameIdFormat.Should().Be(expected,
            "name_id_format is one of the four URNs the auth server accepts");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldThrowGotrueException_GivenUnrecognizedNameIdFormat()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.WithNameIdFormat("urn:example:nameid-format:custom"));
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId));
        await get.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*unrecognized SAML NameID format 'urn:example:nameid-format:custom'*",
            "a format outside the four the auth server accepts is a broken response, not a format to guess at");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldThrowGotrueException_GivenProviderWithoutSamlConfiguration()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.WithoutSaml);
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId));
        await get.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*without its SAML configuration*",
            "SAML is the only provider type the auth server supports, so every provider carries a saml object");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, "null");
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId));
        await get.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no SSO provider*",
            "a 200 always carries the provider; anything else is a broken response, not a provider");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenUnknownProvider()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"sso_provider_not_found","msg":"SSO Identity Provider not found"}""");
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ByResourceId("unknown"));
        await get.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found when no provider matches the id or resource ID");
    }

    [TestMethod]
    public void GetSsoProviderAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, SsoProviderResponses.Complete);
        BlockingCaller.Completes(() => this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId))).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(ProviderId), cancelled.Token);
        await get.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/sso/providers/*")).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
