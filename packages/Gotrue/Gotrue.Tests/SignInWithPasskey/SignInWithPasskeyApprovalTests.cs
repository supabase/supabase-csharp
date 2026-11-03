#region

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.SignInWithPasskey;

#endregion

namespace Gotrue.Tests.SignInWithPasskey;

/// <summary>
///     Pins the exact bytes a passkey sign-in puts on the wire: no body when asking for a challenge without a CAPTCHA
///     token, the token under <c>gotrue_meta_security</c> when there is one, and the challenge id with the assertion
///     passed through verbatim when completing the sign-in.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignInWithPasskeyApprovalTests : RequestApprovalFixture
{
    private const string Challenge = """{"challenge_id":"9a6c2f4e-3b1d-4e8a-a7c5-0f2d6b9e1c38","options":{"challenge":"q1Xb7dQm3N0vLrS8yTzK2wF5pH9jC4aE6uG0oB1iV7M"},"expires_at":1793717400}""";
    private static readonly Guid ChallengeId = Guid.Parse("9a6c2f4e-3b1d-4e8a-a7c5-0f2d6b9e1c38");

    [TestInitialize]
    public void AnswerWithChallenge() => this.RespondTo("/passkeys/authentication/options", Challenge);

    [TestMethod]
    public async Task StartPasskeySignInRequest_ShouldSendNoBody_GivenNoOptions()
    {
        await this.Api.StartPasskeySignInAsync();
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task StartPasskeySignInRequest_ShouldSendNoBody_GivenEmptyCaptchaToken()
    {
        await this.Api.StartPasskeySignInAsync(new SignInWithPasskeyOptions { CaptchaToken = string.Empty });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task StartPasskeySignInRequest_ShouldSendTheCaptchaToken_GivenCaptchaToken()
    {
        await this.Api.StartPasskeySignInAsync(new SignInWithPasskeyOptions { CaptchaToken = "a-captcha-token" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task SignInWithPasskeyRequest_ShouldSendTheChallengeIdAndTheAssertionVerbatim()
    {
        var assertion = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignInWithPasskey", "Data", "assertion.json"))).RootElement;
        await this.Api.SignInWithPasskeyAsync(ChallengeId, assertion);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
