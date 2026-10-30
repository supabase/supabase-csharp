#region

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.UpdateFactor;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.UpdateFactor;

/// <summary>
///     Pins the wire contract of updating a user's MFA factor against a stubbed auth server: the route, the
///     service-role credentials, the response and failure mapping, argument checks, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateFactorContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private const string UserId = "8d3f1c2a-5b6e-4f70-9a81-b2c3d4e5f607";

    private const string FactorId = "2b306a77-21dc-4110-ba71-537cb56b9e98";

    private static readonly FactorChanges Rename = new() { FriendlyName = "Work phone" };

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldPutChangesByUserAndFactorWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.OK, FactorResponses.PhoneFactor);
        await this.Admin.UpdateFactorAsync(UserId, FactorId, Rename);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Put)
            .WithPath($"/admin/users/{UserId}/factors/{FactorId}")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithJsonContentType();
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldSendIdsAsGiven_GivenUppercaseIds()
    {
        this.AnswerWith(HttpStatusCode.OK, FactorResponses.PhoneFactor);
        await this.Admin.UpdateFactorAsync(UserId.ToUpperInvariant(), FactorId.ToUpperInvariant(), Rename);
        this.server.VerifySingleReceivedRequest().WithPath($"/admin/users/{UserId.ToUpperInvariant()}/factors/{FactorId.ToUpperInvariant()}");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldReturnUpdatedFactor()
    {
        this.AnswerWith(HttpStatusCode.OK, FactorResponses.PhoneFactor);
        (await this.Admin.UpdateFactorAsync(UserId, FactorId, new FactorChanges { Phone = "+15555550199" })).Should().BeEquivalentTo(new
        {
            Id = FactorId,
            FriendlyName = "Work phone",
            FactorType = "phone",
            Status = "verified",
            Phone = "15555550199",
        }, "the auth server answers 200 with the factor as stored after the update");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowWithErrorCode_GivenUnknownFactor()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"mfa_factor_not_found","msg":"Factor not found"}""");
        var update = () => this.Admin.UpdateFactorAsync(UserId, FactorId, Rename);
        await update.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "mfa_factor_not_found",
                "the auth server answers 404 mfa_factor_not_found when the user owns no such factor");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowWithErrorCode_GivenPhoneNotInE164()
    {
        this.AnswerWith(HttpStatusCode.BadRequest, """{"code":400,"error_code":"validation_failed","msg":"Invalid phone number format (E.164 required)"}""");
        var update = () => this.Admin.UpdateFactorAsync(UserId, FactorId, new FactorChanges { Phone = "not-a-phone" });
        await update.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "validation_failed",
                "the auth server answers 400 validation_failed to a phone number that is not E.164 on a phone factor");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, "null");
        var update = () => this.Admin.UpdateFactorAsync(UserId, FactorId, Rename);
        await update.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no factor*",
            "a 200 always carries the updated factor; anything else is a broken response, not a factor");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowArgumentNull_GivenNullChanges()
    {
        var update = () => this.Admin.UpdateFactorAsync(UserId, FactorId, null!);
        await update.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("changes");
        this.server.CountReceivedRequests().Should().Be(0, "argument misuse is rejected before any I/O");
    }

    [TestMethod]
    public void UpdateFactorAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, FactorResponses.PhoneFactor);
        BlockingCaller.Completes(() => this.Admin.UpdateFactorAsync(UserId, FactorId, Rename)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var update = () => this.Admin.UpdateFactorAsync(UserId, FactorId, Rename, cancelled.Token);
        await update.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/users/*/factors/*")).UsingPut())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
