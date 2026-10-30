#region

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Mfa;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.UpdateFactor;

#endregion

namespace Gotrue.Tests.UpdateFactor;

/// <summary>
///     End-to-end service-role update of a user's MFA factor against the live stack: renaming a factor, changing the
///     phone number of a phone factor, and the failures the auth server answers with.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class UpdateFactorTests : MfaFixture
{
    private const string EnrolledPhone = "+15555550100";

    private const string NewPhone = "+15555550199";

    private static readonly HttpClient FactorsApi = new();

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldRenameTheFactor()
    {
        var session = await this.SignUpNewUser();
        var enrollment = await this.EnrollTotp();
        var updated = await this.Admin.UpdateFactorAsync(session.User!.Id!, enrollment.Id, new FactorChanges { FriendlyName = "Work authenticator" });
        updated.FriendlyName.Should().Be("Work authenticator", "the auth server applies the new friendly name and answers with the stored factor");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldChangeThePhone_GivenPhoneFactor()
    {
        var session = await this.SignUpNewUser();
        var factorId = await EnrollPhone(session);
        var updated = await this.Admin.UpdateFactorAsync(session.User!.Id!, factorId, new FactorChanges { Phone = NewPhone });
        updated.Phone.Should().Be(NewPhone.TrimStart('+'), "the auth server stores the E.164 number without its leading '+'");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldIgnoreThePhone_GivenTotpFactor()
    {
        var session = await this.SignUpNewUser();
        var enrollment = await this.EnrollTotp();
        var updated = await this.Admin.UpdateFactorAsync(session.User!.Id!, enrollment.Id, new FactorChanges { Phone = NewPhone });
        updated.Phone.Should().BeNullOrEmpty("the auth server ignores a phone number sent for a factor that is not a phone factor");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowValidationFailed_GivenPhoneNotInE164()
    {
        var session = await this.SignUpNewUser();
        var factorId = await EnrollPhone(session);
        var update = () => this.Admin.UpdateFactorAsync(session.User!.Id!, factorId, new FactorChanges { Phone = "not-a-phone" });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "validation_failed", "the auth server answers 400 validation_failed to a phone number that is not E.164");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowFactorNotFound_GivenUnknownFactor()
    {
        var session = await this.SignUpNewUser();
        var update = () => this.Admin.UpdateFactorAsync(session.User!.Id!, Guid.NewGuid().ToString(), new FactorChanges { FriendlyName = "Renamed" });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "mfa_factor_not_found", "the auth server answers 404 mfa_factor_not_found when the user owns no such factor");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowUserNotFound_GivenUnknownUser()
    {
        var update = () => this.Admin.UpdateFactorAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), new FactorChanges { FriendlyName = "Renamed" });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "user_not_found", "the auth server answers 404 user_not_found when no user has the id");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrow_GivenFriendlyNameOfAnotherFactor()
    {
        var session = await this.SignUpNewUser();
        var first = await this.EnrollTotp();
        var second = await this.EnrollTotp("Second authenticator");
        var update = () => this.Admin.UpdateFactorAsync(session.User!.Id!, second.Id, new FactorChanges { FriendlyName = first.FriendlyName });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.StatusCode == 500, "the auth server does not check names on update, so the unique index on (friendly_name, user_id) fails the write");
    }

    [TestMethod]
    public async Task UpdateFactorAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var session = await this.SignUpNewUser();
        var enrollment = await this.EnrollTotp();
        var update = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).UpdateFactorAsync(session.User!.Id!, enrollment.Id, new FactorChanges { FriendlyName = "Renamed" });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired, "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private static async Task<string> EnrollPhone(Session session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestClients.CliAuthUrl}/factors");
        request.Headers.Add("apikey", TestClients.CliAnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        request.Content = new StringContent($$"""{"factor_type":"phone","phone":"{{EnrolledPhone}}"}""", Encoding.UTF8, "application/json");
        using var response = await FactorsApi.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var enrolled = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return enrolled.RootElement.GetProperty("id").GetString()!;
    }
}
