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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.AdminListPasskeys;

/// <summary>
///     Pins how the admin client lists a user's passkeys against a stubbed auth server: the
///     GET /admin/users/{user_id}/passkeys request with the service-role credentials, how the passkey list maps to the
///     result, the failures the server answers with, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class AdminListPasskeysContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid UserId = Guid.Parse("8d3f1c2a-5b6e-4f70-9a81-b2c3d4e5f607");

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldGetTheUsersPasskeysWithServiceRoleCredentials()
    {
        this.AnswerWith(200, Fixture("passkeys.json"));
        await this.Admin.ListPasskeysAsync(UserId);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath($"/admin/users/{UserId}/passkeys")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEachPasskeyInServerOrder_GivenUserWithPasskeys()
    {
        this.AnswerWith(200, Fixture("passkeys.json"));
        (await this.Admin.ListPasskeysAsync(UserId)).Should().BeEquivalentTo(new[]
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
            "each item is a PasskeyListItem (id, friendly_name, created_at, last_used_at), listed in the server's created_at order (supabase/auth AdminPasskeyList)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEmptyList_GivenUserWithoutPasskeys()
    {
        this.AnswerWith(200, Fixture("no_passkeys.json"));
        (await this.Admin.ListPasskeysAsync(UserId)).Should().BeEmpty(
            "the auth server answers an empty array for a user without passkeys (supabase/auth AdminPasskeyList)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEmptyList_GivenSuccessWithoutBody()
    {
        this.AnswerWith(200, "null");
        (await this.Admin.ListPasskeysAsync(UserId)).Should().BeEmpty("a collection on the public surface is never null");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldThrowUserNotFound_GivenUnknownUser()
    {
        this.AnswerWith(404, Fixture("user_not_found_error.json"));
        var listing = () => this.Admin.ListPasskeysAsync(UserId);
        (await listing.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(UserNotFound,
            "the auth server answers 404 user_not_found when no user has the id (supabase/auth loadUser)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(403, Fixture("not_admin_error.json"));
        var listing = () => this.Admin.ListPasskeysAsync(UserId);
        (await listing.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim (supabase/auth requireAdmin)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var listing = () => this.Admin.ListPasskeysAsync(UserId, cancelled.Token);
        await listing.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    [TestMethod]
    public void ListPasskeysAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(200, Fixture("passkeys.json"));
        BlockingCaller.Completes(() => this.Admin.ListPasskeysAsync(UserId)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath($"/admin/users/{UserId}/passkeys").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AdminListPasskeys", "Data", name));
}
