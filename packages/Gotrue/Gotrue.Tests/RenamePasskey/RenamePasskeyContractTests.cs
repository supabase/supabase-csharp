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

namespace Gotrue.Tests.RenamePasskey;

/// <summary>
///     Pins how RenamePasskeyAsync renames one of the signed-in user's passkeys: the PATCH /passkeys/{passkey_id}
///     request it puts on the wire, how the updated passkey maps to the result, and the failures raised before and
///     after the request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RenamePasskeyContractTests
{
    private const string AccessToken = "an-access-token";
    private const string NewName = "Work laptop";
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
    public async Task RenamePasskeyAsync_ShouldPatchThePasskeyWithTheSessionBearer()
    {
        this.SignIn();
        this.MockPatchResponse(200, Fixture("renamed_passkey.json"));
        await this.client.RenamePasskeyAsync(PasskeyId, NewName);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Patch)
            .WithPath(PasskeyPath)
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldReturnTheUpdatedPasskey_GivenUserOwnsIt()
    {
        this.SignIn();
        this.MockPatchResponse(200, Fixture("renamed_passkey.json"));
        var renamed = await this.client.RenamePasskeyAsync(PasskeyId, NewName);
        renamed.Should().Be(new Passkey
        {
            Id = PasskeyId,
            FriendlyName = NewName,
            CreatedAt = new DateTimeOffset(2026, 3, 2, 9, 15, 30, TimeSpan.Zero).AddTicks(1234560),
            LastUsedAt = new DateTimeOffset(2026, 10, 28, 17, 42, 5, TimeSpan.Zero),
        }, "the server answers with the renamed PasskeyListItem (id, friendly_name, created_at, last_used_at) (supabase/auth PasskeyUpdate)");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldThrow_GivenSuccessWithoutAPasskey()
    {
        this.SignIn();
        this.MockPatchResponse(200, "null");
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, NewName);
        await renaming.Should().ThrowAsync<GotrueException>().WithMessage("The auth server returned no passkey.",
            "a rename always answers with the updated passkey, so its absence is a server failure");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldThrowNotFound_GivenPasskeyTheUserDoesNotOwn()
    {
        this.SignIn();
        this.MockPatchResponse(404, Fixture("passkey_not_found_error.json"));
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, NewName);
        (await renaming.Should().ThrowAsync<GotrueException>()).Which.StatusCode.Should().Be(404,
            "a passkey the user does not own is reported as not found (supabase/auth PasskeyUpdate)");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldThrowServerRejection_GivenNameTheServerRejects()
    {
        this.SignIn();
        this.MockPatchResponse(400, Fixture("friendly_name_too_long_error.json"));
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, NewName);
        (await renaming.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("validation_failed",
            "the name length limit is the server's to enforce (supabase/auth PasskeyUpdate)");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldThrowServerRejection_GivenPasskeysDisabled()
    {
        this.SignIn();
        this.MockPatchResponse(404, Fixture("passkeys_disabled_error.json"));
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, NewName);
        (await renaming.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("passkey_disabled",
            "every /passkeys route answers 404 passkey_disabled when passkeys are off on the server (supabase/auth requirePasskeyEnabled)");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task RenamePasskeyAsync_ShouldThrowArgumentException_GivenNoName(string? friendlyName)
    {
        this.SignIn();
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, friendlyName!);
        (await renaming.Should().ThrowAsync<ArgumentException>().WithMessage("A passkey name is required.*")).Which.ParamName.Should().Be("friendlyName",
            "the server rejects an empty friendly_name (supabase/auth PasskeyUpdate), so the SDK reports it before the wire");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldNotSendARequest_GivenNoName()
    {
        this.SignIn();
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, string.Empty);
        await renaming.Should().ThrowAsync<ArgumentException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing name is detected before the wire");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, NewName);
        (await renaming.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "PATCH /passkeys/{passkey_id} requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, NewName);
        (await renaming.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "PATCH /passkeys/{passkey_id} requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, NewName);
        await renaming.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockPatchResponse(200, Fixture("renamed_passkey.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var renaming = () => this.client.RenamePasskeyAsync(PasskeyId, NewName, cancellation.Token);
        await renaming.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void RenamePasskeyAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockPatchResponse(200, Fixture("renamed_passkey.json"));
        BlockingCaller.Completes(() => this.client.RenamePasskeyAsync(PasskeyId, NewName)).Should().BeTrue(
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

    private void MockPatchResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath(PasskeyPath).UsingPatch())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RenamePasskey", "Data", name));
}
