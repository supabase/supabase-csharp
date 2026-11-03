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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.AdminDeletePasskey;

/// <summary>
///     Pins how the admin client deletes one of a user's passkeys against a stubbed auth server: the
///     DELETE /admin/users/{user_id}/passkeys/{passkey_id} request with the service-role credentials, the bodiless
///     success, the failures the server answers with, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class AdminDeletePasskeyContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid UserId = Guid.Parse("8d3f1c2a-5b6e-4f70-9a81-b2c3d4e5f607");

    private static readonly Guid PasskeyId = Guid.Parse("5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24");

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldDeleteThePasskeyOfTheUserWithServiceRoleCredentials()
    {
        this.AnswerWith(204, string.Empty);
        await this.Admin.DeletePasskeyAsync(userId: UserId, passkeyId: PasskeyId);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithPath($"/admin/users/{UserId}/passkeys/{PasskeyId}")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowNotFound_GivenPasskeyTheUserDoesNotOwn()
    {
        this.AnswerWith(404, Fixture("passkey_not_found_error.json"));
        var deleting = () => this.Admin.DeletePasskeyAsync(userId: UserId, passkeyId: PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("validation_failed",
            "the auth server answers 404 validation_failed when the user owns no passkey with the id (supabase/auth AdminPasskeyDelete)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowUserNotFound_GivenUnknownUser()
    {
        this.AnswerWith(404, Fixture("user_not_found_error.json"));
        var deleting = () => this.Admin.DeletePasskeyAsync(userId: UserId, passkeyId: PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(UserNotFound,
            "the auth server answers 404 user_not_found when no user has the id (supabase/auth loadUser)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(403, Fixture("not_admin_error.json"));
        var deleting = () => this.Admin.DeletePasskeyAsync(userId: UserId, passkeyId: PasskeyId);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim (supabase/auth requireAdmin)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var deleting = () => this.Admin.DeletePasskeyAsync(userId: UserId, passkeyId: PasskeyId, cancellationToken: cancelled.Token);
        await deleting.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    [TestMethod]
    public void DeletePasskeyAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(204, string.Empty);
        BlockingCaller.Completes(() => this.Admin.DeletePasskeyAsync(userId: UserId, passkeyId: PasskeyId)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath($"/admin/users/{UserId}/passkeys/{PasskeyId}").UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AdminDeletePasskey", "Data", name));
}
