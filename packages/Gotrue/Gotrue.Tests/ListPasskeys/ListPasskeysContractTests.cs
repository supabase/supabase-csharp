#region

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.ListPasskeys;

/// <summary>
///     Pins how ListPasskeysAsync lists the signed-in user's passkeys: the GET /passkeys request it puts on the
///     wire, how the passkey list maps to the result, and the failures raised before and after the request, all
///     against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListPasskeysContractTests
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
    public async Task ListPasskeysAsync_ShouldGetThePasskeysWithTheSessionBearer()
    {
        this.SignIn();
        this.MockPasskeysResponse(200, Fixture("passkeys.json"));
        await this.client.ListPasskeysAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/passkeys")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEachPasskeyInServerOrder_GivenUserWithPasskeys()
    {
        this.SignIn();
        this.MockPasskeysResponse(200, Fixture("passkeys.json"));
        var passkeys = await this.client.ListPasskeysAsync();
        passkeys.Should().BeEquivalentTo(new[]
        {
            new Passkey
            {
                Id = Guid.Parse("5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24"),
                FriendlyName = "iCloud Keychain",
                CreatedAt = new DateTimeOffset(2026, 3, 2, 9, 15, 30, TimeSpan.Zero).AddTicks(1234560),
                LastUsedAt = new DateTimeOffset(2026, 10, 28, 17, 42, 5, TimeSpan.Zero),
            },
            new Passkey
            {
                Id = Guid.Parse("c3a9e2d7-41b6-4f08-8e5a-9d1c7b2f6e30"),
                CreatedAt = new DateTimeOffset(2026, 4, 11, 8, 0, 0, TimeSpan.Zero),
            },
        }, options => options.WithStrictOrdering(),
            "each item is a PasskeyListItem (id, friendly_name, created_at, last_used_at), listed in the server's created_at order (supabase/auth PasskeyList)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEmptyList_GivenUserWithoutPasskeys()
    {
        this.SignIn();
        this.MockPasskeysResponse(200, Fixture("no_passkeys.json"));
        var passkeys = await this.client.ListPasskeysAsync();
        passkeys.Should().BeEmpty("GET /passkeys answers an empty array for a user without passkeys (supabase/auth PasskeyList)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEmptyList_GivenSuccessWithoutBody()
    {
        this.SignIn();
        this.MockPasskeysResponse(200, "null");
        var passkeys = await this.client.ListPasskeysAsync();
        passkeys.Should().BeEmpty("a collection on the public surface is never null");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldThrowServerRejection_GivenPasskeysDisabled()
    {
        this.SignIn();
        this.MockPasskeysResponse(404, Fixture("passkeys_disabled_error.json"));
        var listing = () => this.client.ListPasskeysAsync();
        (await listing.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("passkey_disabled",
            "every /passkeys route answers 404 passkey_disabled when passkeys are off on the server (supabase/auth requirePasskeyEnabled)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var listing = () => this.client.ListPasskeysAsync();
        (await listing.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "GET /passkeys requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var listing = () => this.client.ListPasskeysAsync();
        (await listing.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "GET /passkeys requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var listing = () => this.client.ListPasskeysAsync();
        await listing.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockPasskeysResponse(200, Fixture("passkeys.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var listing = () => this.client.ListPasskeysAsync(cancellation.Token);
        await listing.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void ListPasskeysAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockPasskeysResponse(200, Fixture("passkeys.json"));
        BlockingCaller.Completes(() => this.client.ListPasskeysAsync()).Should().BeTrue(
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

    private void MockPasskeysResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath("/passkeys").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ListPasskeys", "Data", name));
}
