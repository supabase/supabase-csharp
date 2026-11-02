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
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.DeletePasskey;

/// <summary>
///     Pins how DeletePasskeyAsync revokes one of the signed-in user's passkeys: the DELETE /passkeys/{passkey_id}
///     request it puts on the wire, the empty success answer, and the failures raised before and after the
///     request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeletePasskeyContractTests
{
    private const string AccessToken = "an-access-token";
    private const string PasskeyPath = "/passkeys/5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24";
    private static readonly Guid PasskeyId = Guid.Parse("5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24");
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
    public async Task DeletePasskeyAsync_ShouldDeleteThePasskeyWithTheSessionBearer()
    {
        this.SignIn();
        this.MockDeleteResponse(204);
        await this.client.DeletePasskeyAsync(PasskeyId);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithPath(PasskeyPath)
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldComplete_GivenUserOwnsThePasskey()
    {
        this.SignIn();
        this.MockDeleteResponse(204);
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId);
        await deleting.Should().NotThrowAsync("GoTrue answers 204 without a body once the passkey is deleted (supabase/auth PasskeyDelete)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowServerRejection_GivenMfaUserBelowAal2()
    {
        this.SignIn();
        this.MockDeleteResponse(403, Fixture("insufficient_aal_error.json"));
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("insufficient_aal",
            "a user with MFA enabled needs an aal2 session to delete a passkey (supabase/auth requirePasskeyManagementAAL)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowNotFound_GivenPasskeyTheUserDoesNotOwn()
    {
        this.SignIn();
        this.MockDeleteResponse(404, Fixture("passkey_not_found_error.json"));
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.StatusCode.Should().Be(404,
            "a passkey the user does not own is reported as not found rather than treated as already deleted (supabase/auth PasskeyDelete)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowServerRejection_GivenPasskeysDisabled()
    {
        this.SignIn();
        this.MockDeleteResponse(404, Fixture("passkeys_disabled_error.json"));
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("passkey_disabled",
            "every /passkeys route answers 404 passkey_disabled when passkeys are off on the server (supabase/auth requirePasskeyEnabled)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "DELETE /passkeys/{passkey_id} requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "DELETE /passkeys/{passkey_id} requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId);
        await deleting.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowOffline_GivenClientIsOffline()
    {
        this.SignIn();
        this.client.Online = false;
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>().WithMessage("Only supported when online")).Which.Reason.Should().Be(Offline,
            "the passkey is deleted on the server");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockDeleteResponse(204);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var deleting = () => this.client.DeletePasskeyAsync(PasskeyId, cancellation.Token);
        await deleting.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void DeletePasskeyAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockDeleteResponse(204);
        BlockingCaller.Completes(() => this.client.DeletePasskeyAsync(PasskeyId)).Should().BeTrue(
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

    private void MockDeleteResponse(int statusCode) =>
        this.server
            .Given(Request.Create().WithPath(PasskeyPath).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(statusCode));

    private void MockDeleteResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath(PasskeyPath).UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeletePasskey", "Data", name));
}
