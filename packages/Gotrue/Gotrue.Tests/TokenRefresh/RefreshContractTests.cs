#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants.AuthState;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.TokenRefresh;

/// <summary>
///     Pins the wire contract of an explicit token refresh: the request the SDK puts on the wire, how a
///     successful response becomes the current session, and how error responses are classified — all against
///     a stubbed server so the interaction is exercised without the live stack.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RefreshContractTests
{
    private const string AccessToken = "an-access-token";
    private const string RefreshTokenValue = "a-refresh-token";
    private IGotrueClient<User, Session> client = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
        var persistence = SessionPersistenceSubstitute.Tracking();
        persistence.SaveSession(new Session { AccessToken = AccessToken, RefreshToken = RefreshTokenValue, ExpiresIn = 3600 });
        this.client.SetPersistence(persistence);
        this.client.LoadSession();
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task RefreshToken_ShouldPostRefreshTokenGrantWithBearerAndApiKey()
    {
        this.MockSuccessResponse();
        await this.client.RefreshToken(AccessToken, RefreshTokenValue);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/token")
            .WithQueryParam("grant_type", "refresh_token")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey)
            .WithJsonContentType()
            .WithExactJsonBody("refresh_token", RefreshTokenValue);
    }

    [TestMethod]
    public async Task RefreshToken_ShouldBecomeTheCurrentSession_GivenSuccess()
    {
        this.MockSuccessResponse();
        await this.client.RefreshToken(AccessToken, RefreshTokenValue);
        this.client.CurrentSession.Should().NotBeNull();
        this.client.CurrentSession!.AccessToken.Should().Be("new-access-token");
        this.client.CurrentSession!.RefreshToken.Should().Be("new-refresh-token");
        this.client.CurrentSession!.User!.Id.Should().Be("user-id-123");
        this.client.CurrentSession!.ExpiresIn.Should().Be(3600);
    }

    [TestMethod]
    [DataRow("token_not_found_error.json", DisplayName = "unknown token (refresh_token_not_found)")]
    [DataRow("malformed_token_error.json", DisplayName = "malformed token (validation_failed)")]
    public async Task RefreshToken_ShouldThrowInvalidRefreshTokenAndDestroySession_GivenRejected(string fixture)
    {
        this.MockErrorResponse(400, Fixture(fixture));
        var refresh = () => this.client.RefreshToken(AccessToken, RefreshTokenValue);
        var exception = await refresh.Should().ThrowAsync<GotrueException>();
        exception.Which.Reason.Should().Be(InvalidRefreshToken);
        this.client.CurrentSession.Should().BeNull();
    }

    [TestMethod]
    public async Task RefreshToken_ShouldNotifySignedOut_GivenRejected()
    {
        var stateChanges = new List<Constants.AuthState>();
        this.client.AddStateChangedListener((_, state) => stateChanges.Add(state));
        this.MockErrorResponse(400, Fixture("token_not_found_error.json"));
        var refresh = () => this.client.RefreshToken(AccessToken, RefreshTokenValue);
        await refresh.Should().ThrowAsync<GotrueException>();
        stateChanges.Should().ContainSingle(state => state == SignedOut,
            "a rejected refresh must notify listeners the session ended — the auto-refresh timer swallows the exception, so the SignedOut event is the only way a background refresh failure reaches the app (issue #91)");
    }

    [TestMethod]
    public async Task RefreshToken_ShouldThrowUnknownAndKeepSession_GivenUnrecognizedError()
    {
        this.MockErrorResponse(500, Fixture("unclassified_error.json"));
        var refresh = () => this.client.RefreshToken(AccessToken, RefreshTokenValue);
        var exception = await refresh.Should().ThrowAsync<GotrueException>();
        exception.Which.Reason.Should().Be(Unknown);
        this.client.CurrentSession.Should().NotBeNull();
    }

    [TestMethod]
    public async Task RefreshToken_ShouldKeepTheCurrentSession_GivenAForeignTokenIsRejected()
    {
        var stateChanges = new List<Constants.AuthState>();
        this.client.AddStateChangedListener((_, state) => stateChanges.Add(state));
        this.MockErrorResponse(400, Fixture("token_not_found_error.json"));
        var refresh = () => this.client.RefreshToken("another-access-token", "another-refresh-token");
        await refresh.Should().ThrowAsync<GotrueException>();
        this.client.CurrentSession!.RefreshToken.Should().Be(RefreshTokenValue,
            "a refresh rejected for another session must not sign the current one out (issue #396)");
        stateChanges.Should().NotContain(SignedOut);
    }

    private void MockSuccessResponse() =>
        this.server
            .Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(Fixture("token_success.json")));

    private void MockErrorResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TokenRefresh", "Fixtures", name));
}
