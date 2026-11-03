#region

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.SignInWithPasskey;

/// <summary>
///     Pins that the stateless SignInWithPasskeyAsync twin on the API never hands back an unusable session: an
///     answer without a session, or with a session missing its access token, fails instead of returning, all against a
///     stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignInWithPasskeyApiContractTests
{
    private const string VerifyPath = "/passkeys/authentication/verify";
    private static readonly Guid ChallengeId = Guid.Parse("9a6c2f4e-3b1d-4e8a-a7c5-0f2d6b9e1c38");
    private IGotrueApi<User, Session> api = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.api = new Api(this.server.Url);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowUnknown_GivenSuccessWithoutBody()
    {
        this.MockVerifyResponse("null");
        var signingIn = () => this.api.SignInWithPasskeyAsync(ChallengeId, Assertion());
        (await signingIn.Should().ThrowAsync<GotrueException>().WithMessage("The auth server returned no session for the passkey sign-in.")).Which.Reason.Should().Be(Unknown,
            "a 200 answer must carry the session, so the twin returns a session or fails");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowUnknown_GivenSessionWithoutAccessToken()
    {
        this.MockVerifyResponse(Fixture("session_without_access_token.json"));
        var signingIn = () => this.api.SignInWithPasskeyAsync(ChallengeId, Assertion());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(Unknown,
            "a session without an access token cannot stand for the signed-in user");
    }

    private static JsonElement Assertion() => JsonDocument.Parse(Fixture("assertion.json")).RootElement;

    private void MockVerifyResponse(string body) =>
        this.server
            .Given(Request.Create().WithPath(VerifyPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignInWithPasskey", "Data", name));
}
