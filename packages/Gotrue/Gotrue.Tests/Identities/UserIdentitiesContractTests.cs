#region

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
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

namespace Gotrue.Tests.Identities;

/// <summary>
///     Pins how GetUserIdentitiesAsync reads the signed-in user's linked identities: the GET /user request
///     it puts on the wire, how the user's identities array maps to the result, and the failures raised before
///     and after the request — all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UserIdentitiesContractTests
{
    private const string AccessToken = "an-access-token";
    private IGotrueClient<User, Session> client = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldGetTheUserWithTheSessionBearer()
    {
        this.SignIn();
        this.MockUserResponse(200, Fixture("user_with_identities.json"));
        await this.client.GetUserIdentitiesAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/user")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldReturnEveryLinkedIdentity_GivenUserWithIdentities()
    {
        this.SignIn();
        this.MockUserResponse(200, Fixture("user_with_identities.json"));
        var identities = await this.client.GetUserIdentitiesAsync();
        identities.Select(identity => identity.IdentityId).Should().Equal(new[] { "identity-email-1", "identity-github-1" },
            "the result is the identities array of the user GoTrue returns from GET /user, in order, not the cached session user who has none");
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldReturnEmpty_GivenUserWithNullIdentities()
    {
        this.SignIn();
        this.MockUserResponse(200, Fixture("user_with_null_identities.json"));
        var identities = await this.client.GetUserIdentitiesAsync();
        identities.Should().BeEmpty("collections are never null: an absent identities array means no linked identity");
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldReturnEmpty_GivenNullUserBody()
    {
        this.SignIn();
        this.MockUserResponse(200, "null");
        var identities = await this.client.GetUserIdentitiesAsync();
        identities.Should().BeEmpty("collections are never null: a response without a user means no linked identity");
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldThrowServerRejection_GivenExpiredJwt()
    {
        this.SignIn();
        this.MockUserResponse(403, Fixture("expired_jwt_error.json"));
        var listing = () => this.client.GetUserIdentitiesAsync();
        (await listing.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("bad_jwt",
            "GoTrue rejects an expired access token on GET /user with error_code bad_jwt");
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var listing = () => this.client.GetUserIdentitiesAsync();
        (await listing.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "GET /user requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var listing = () => this.client.GetUserIdentitiesAsync();
        (await listing.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "GET /user requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var listing = () => this.client.GetUserIdentitiesAsync();
        await listing.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldThrowOffline_GivenClientIsOffline()
    {
        this.SignIn();
        this.client.Online = false;
        var listing = () => this.client.GetUserIdentitiesAsync();
        (await listing.Should().ThrowAsync<GotrueException>().WithMessage("Only supported when online")).Which.Reason.Should().Be(Offline,
            "reading the identities needs the server, like the other identity-linking operations");
    }

    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockUserResponse(200, Fixture("user_with_identities.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var listing = () => this.client.GetUserIdentitiesAsync(cancellation.Token);
        await listing.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void GetUserIdentitiesAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockUserResponse(200, Fixture("user_with_identities.json"));
        BlockingCaller.Completes(() => this.client.GetUserIdentitiesAsync()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    private void SignIn() => this.SignIn(new Session { AccessToken = AccessToken, RefreshToken = "a-refresh-token", ExpiresIn = 3600, User = new User { Id = "user-id-123" } });

    private void SignIn(Session session)
    {
        var persistence = SessionPersistenceSubstitute.Tracking();
        persistence.SaveSession(session);
        this.client.SetPersistence(persistence);
        this.client.LoadSession();
    }

    private void MockUserResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath("/user").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Identities", "Data", name));
}
