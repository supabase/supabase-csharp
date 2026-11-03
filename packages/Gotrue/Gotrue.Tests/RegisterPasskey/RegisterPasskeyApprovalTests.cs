#region

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace Gotrue.Tests.RegisterPasskey;

/// <summary>
///     Pins the exact bytes a passkey registration puts on the wire: no body when asking for a challenge, since the
///     endpoint reads none, and the challenge id with the credential passed through verbatim when completing it.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RegisterPasskeyApprovalTests : RequestApprovalFixture
{
    private const string Challenge = """{"challenge_id":"4d8e1b7a-6c2f-4a9e-b3d5-7f0c1e2a9b64","options":{"challenge":"Zr8tY2pVq0xW4nL7cK1sB9dH3fJ6mA5eT0uG2oR8iE4"},"expires_at":1793717400}""";
    private const string RegisteredPasskey = """{"id":"5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24","friendly_name":"iCloud Keychain","created_at":"2026-11-03T14:45:12Z"}""";
    private static readonly Guid ChallengeId = Guid.Parse("4d8e1b7a-6c2f-4a9e-b3d5-7f0c1e2a9b64");

    [TestInitialize]
    public void AnswerWithChallengeAndPasskey()
    {
        this.RespondTo("/passkeys/registration/options", Challenge);
        this.RespondTo("/passkeys/registration/verify", RegisteredPasskey);
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationRequest_ShouldSendNoBody()
    {
        await this.Api.StartPasskeyRegistrationAsync("user-jwt");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task RegisterPasskeyRequest_ShouldSendTheChallengeIdAndTheCredentialVerbatim()
    {
        var credential = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RegisterPasskey", "Data", "credential.json"))).RootElement;
        await this.Api.RegisterPasskeyAsync("user-jwt", ChallengeId, credential);
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
