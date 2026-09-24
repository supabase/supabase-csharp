#region

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Claims;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;
using JwtHeader = System.IdentityModel.Tokens.Jwt.JwtHeader;

#endregion

namespace Gotrue.Tests.Claims;

/// <summary>
///     Pins how GetClaimsAsync verifies a token: locally against the server's published key set when the
///     signing key is asymmetric, through GET /user otherwise (issues #427 and #262).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetClaimsTests
{
    private const string JwksPath = "/.well-known/jwks.json";
    private const string UserPath = "/user";
    private static readonly DateTime FutureExpiry = DateTime.UtcNow.AddHours(1);
    private static readonly DateTime PastExpiry = DateTime.UtcNow.AddHours(-1);

    private readonly List<string> publishedKeys = new();

    private MockGotrueServer server = null!;
    private IGotrueClient<User, Session> client = null!;
    private SigningCredentials publishedCredentials = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
        this.publishedCredentials = this.PublishSigningKey("ES256", "key-1");
        this.server.Given(Request.Create().WithPath(UserPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
        this.server.Given(Request.Create().WithPath(JwksPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody(_ => $$"""{"keys":[{{string.Join(",", this.publishedKeys)}}]}"""));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    [DataRow("ES256")]
    [DataRow("RS256")]
    public async Task GetClaimsAsync_ShouldVerifyLocally_GivenAsymmetricKey(string alg)
    {
        var response = await this.client.GetClaimsAsync(GenerateToken(this.PublishSigningKey(alg, "key-2"), FutureExpiry));
        using (new AssertionScope())
        {
            response.Claims.Sub.Should().Be("user-123");
            response.Header.Alg.Should().Be(alg);
            response.Claims.Aud.Should().Equal(new[] { "authenticated" }, "aud also accepts a single string, RFC 7519 §4.1.3");
            this.server.CountReceivedRequests(UserPath).Should().Be(0, "asymmetric tokens are verified locally (issue #427)");
        }
    }

    [TestMethod]
    public async Task GetClaimsAsync_ShouldReturnClaims_GivenExpiredTokenAndAllowExpired()
    {
        var response = await this.client.GetClaimsAsync(GenerateToken(this.publishedCredentials, PastExpiry), new GetClaimsOptions { AllowExpired = true });
        response.Claims.Sub.Should().Be("user-123", "AllowExpired skips expiration validation");
    }

    [TestMethod]
    public async Task GetClaimsAsync_ShouldReadClaimsAsSigned_GivenCaseDistinctClaimsAndArrayAudience()
    {
        var token = WriteToken(this.publishedCredentials, new JwtPayload
        {
            ["sub"] = "user-123",
            ["SUB"] = "custom-value",
            ["role"] = "authenticated",
            ["ROLE"] = "service_role",
            ["aud"] = new[] { "authenticated", "api" },
            ["exp"] = EpochTime.GetIntDate(FutureExpiry),
        });
        var response = await this.client.GetClaimsAsync(token);
        using (new AssertionScope())
        {
            response.Claims.Sub.Should().Be("user-123", "claim names are case sensitive, RFC 7519 §7.3");
            response.Claims.Role.Should().Be("authenticated");
            response.Claims.AdditionalClaims.Keys.Should().Contain(new[] { "SUB", "ROLE" });
            response.Claims.Aud.Should().Equal(new[] { "authenticated", "api" }, "aud may be an array, RFC 7519 §4.1.3");
        }
    }

    [TestMethod]
    [DataRow("HS256", "key-1", 0)]
    [DataRow("ES256", null, 0)]
    [DataRow("ES256", "key-2", 1)]
    public async Task GetClaimsAsync_ShouldFallBackToServer_GivenNoLocalKey(string alg, string? kid, int jwksFetches)
    {
        var credentials = alg == "HS256" ? SymmetricKey(kid) : GenerateSigningKey(alg, kid).Credentials;
        await this.client.GetClaimsAsync(GenerateToken(credentials, FutureExpiry));
        using (new AssertionScope())
        {
            this.server.CountReceivedRequests(JwksPath).Should().Be(jwksFetches, "JWKS lookup requires a supported algorithm and a key id");
            this.server.CountReceivedRequests(UserPath).Should().Be(1, "tokens without a local key require server verification");
        }
    }

    [TestMethod]
    public async Task GetClaimsAsync_ShouldFallBackToServer_GivenNullKeyEntry()
    {
        this.publishedKeys[0] = "null";
        await this.client.GetClaimsAsync(GenerateToken(this.publishedCredentials, FutureExpiry));
        this.server.CountReceivedRequests(UserPath).Should().Be(1, "a null key entry cannot be used for local verification");
    }

    [TestMethod]
    [DataRow("expired")]
    [DataRow("no exp")]
    [DataRow("malformed")]
    public async Task GetClaimsAsync_ShouldThrowInvalidJwt_GivenUnverifiableToken(string scenario)
    {
        var token = scenario switch
        {
            "expired" => GenerateToken(this.publishedCredentials, PastExpiry),
            "no exp" => GenerateToken(this.publishedCredentials, null),
            "malformed" => "not-a-jwt",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
        var getClaims = () => this.client.GetClaimsAsync(token);
        using (new AssertionScope())
        {
            (await getClaims.Should().ThrowAsync<GotrueException>())
                .Which.Reason.Should().Be(InvalidJwt, "invalid tokens have a specific failure reason (issue #262)");
            this.server.CountReceivedRequests().Should().Be(0, "local validation precedes network requests");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GetClaimsAsync_ShouldThrowInvalidJwt_GivenForeignSignature(bool allowExpired)
    {
        var token = GenerateToken(GenerateSigningKey("ES256", "key-1").Credentials, allowExpired ? PastExpiry : FutureExpiry);
        var getClaims = () => this.client.GetClaimsAsync(token, new GetClaimsOptions { AllowExpired = allowExpired });
        using (new AssertionScope())
        {
            (await getClaims.Should().ThrowAsync<GotrueException>())
                .Which.Reason.Should().Be(InvalidJwt);
            this.server.CountReceivedRequests(UserPath).Should().Be(0, "invalid signatures must not trigger server fallback");
        }
    }

    [TestMethod]
    [DataRow("exp", "1700000000")]
    [DataRow("aud", null)]
    [DataRow("aud", new object?[] { "authenticated", null })]
    public async Task GetClaimsAsync_ShouldThrowInvalidJwt_GivenUnreadableClaimValue(string claim, object? value)
    {
        var token = WriteToken(this.publishedCredentials, new JwtPayload
        {
            ["sub"] = "user-123",
            ["exp"] = EpochTime.GetIntDate(FutureExpiry),
            [claim] = value,
        });
        var getClaims = () => this.client.GetClaimsAsync(token);
        (await getClaims.Should().ThrowAsync<GotrueException>("claim conversion errors use the SDK error contract"))
            .Which.Reason.Should().Be(InvalidJwt);
    }

    [TestMethod]
    public async Task GetClaimsAsync_ShouldReuseCachedJwks()
    {
        var token = GenerateToken(this.publishedCredentials, FutureExpiry);
        await this.client.GetClaimsAsync(token);
        await this.client.GetClaimsAsync(token);
        this.server.CountReceivedRequests(JwksPath).Should().Be(1, "fresh keys are cached");
    }

    [TestMethod]
    public async Task GetClaimsAsync_ShouldRefetchJwks_GivenUnknownKid()
    {
        await this.client.GetClaimsAsync(GenerateToken(this.publishedCredentials, FutureExpiry));
        await this.client.GetClaimsAsync(GenerateToken(this.PublishSigningKey("ES256", "key-2"), FutureExpiry));
        using (new AssertionScope())
        {
            this.server.CountReceivedRequests(JwksPath).Should().Be(2, "an unknown kid refreshes the cache");
            this.server.CountReceivedRequests(UserPath).Should().Be(0, "the rotated key is verified locally");
        }
    }

    [TestMethod]
    public async Task GetClaimsAsync_ShouldSkipNetwork_GivenSuppliedJwks()
    {
        var key = JsonSerializer.Deserialize<Jwk>(this.publishedKeys[0])!;
        var options = new GetClaimsOptions
        {
            Jwks = new Jwks { Keys = new[] { key } },
        };
        await this.client.GetClaimsAsync(GenerateToken(this.publishedCredentials, FutureExpiry), options);
        this.server.CountReceivedRequests().Should().Be(0, "supplied keys take precedence");
    }

    [TestMethod]
    public async Task GetClaimsAsync_ShouldThrowNoSessionFound_GivenNoTokenAndNoSession()
    {
        var getClaims = () => this.client.GetClaimsAsync();
        (await getClaims.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound);
    }

    private static (SigningCredentials Credentials, string Json) GenerateSigningKey(string alg, string? kid)
    {
        if (alg == "RS256")
        {
            var rsa = RSA.Create(2048);
            var rsaParameters = rsa.ExportParameters(false);
            var n = Base64UrlEncoder.Encode(rsaParameters.Modulus);
            var e = Base64UrlEncoder.Encode(rsaParameters.Exponent);
            return (new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = kid }, SecurityAlgorithms.RsaSha256),
                $$"""{"kty":"RSA","kid":"{{kid}}","alg":"RS256","n":"{{n}}","e":"{{e}}"}""");
        }
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var ecParameters = ecdsa.ExportParameters(false);
        var x = Base64UrlEncoder.Encode(ecParameters.Q.X);
        var y = Base64UrlEncoder.Encode(ecParameters.Q.Y);
        return (new SigningCredentials(new ECDsaSecurityKey(ecdsa) { KeyId = kid }, SecurityAlgorithms.EcdsaSha256),
            $$"""{"kty":"EC","kid":"{{kid}}","alg":"ES256","crv":"P-256","x":"{{x}}","y":"{{y}}"}""");
    }

    private static SigningCredentials SymmetricKey(string? kid) =>
        new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestClients.CliJwtSecret)) { KeyId = kid }, SecurityAlgorithms.HmacSha256);

    private SigningCredentials PublishSigningKey(string alg, string? kid)
    {
        var key = GenerateSigningKey(alg, kid);
        this.publishedKeys.Add(key.Json);
        return key.Credentials;
    }

    private static string GenerateToken(SigningCredentials credentials, DateTime? expires)
    {
        var payload = new JwtPayload
        {
            ["sub"] = "user-123",
            ["role"] = "authenticated",
            ["aud"] = "authenticated",
        };
        if (expires != null)
        {
            payload["exp"] = EpochTime.GetIntDate(expires.Value);
        }
        return WriteToken(credentials, payload);
    }

    private static string WriteToken(SigningCredentials credentials, JwtPayload payload)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }
}
