#region

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     End-to-end sign-up against the live stack: registering with an email, with a phone (carrying user metadata
///     or over WhatsApp), with the PKCE flow, or anonymously, and the signed-in session each produces.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SignUpTests : AuthClientFixture
{
    [TestMethod]
    public async Task SignUp_ShouldCreateSignedInSession_GivenEmailAndPassword()
    {
        this.StateChanges.Should().BeEmpty();
        var signedUp = await this.Client.SignUpAsync(Constants.SignUpType.Email, RandomEmail(), Password);
        this.VerifyGoodSession(signedUp.Session);
    }

    [TestMethod]
    public async Task SignUp_ShouldCreateSignedInSessionWithMetadata_GivenPhoneAndPassword()
    {
        this.StateChanges.Should().BeEmpty();
        var signedUp = await this.Client.SignUpAsync(Constants.SignUpType.Phone, GetRandomPhoneNumber(), Password,
            new SignUpOptions { Data = new Dictionary<string, object> { { "firstName", "Testing" } } });
        this.VerifyGoodSession(signedUp.Session);
        signedUp.User!.UserMetadata["firstName"].Should().Be("Testing");
    }

    [TestMethod]
    public async Task SignUp_ShouldCreateSignedInSession_GivenPhoneAndTheWhatsAppChannel()
    {
        var signedUp = await this.Client.SignUpAsync(Constants.SignUpType.Phone, GetRandomPhoneNumber(), Password,
            new SignUpOptions { Channel = SignInWithPasswordlessPhoneOptions.MessagingChannel.WHATSAPP });
        this.VerifyGoodSession(signedUp.Session);
    }

    [TestMethod]
    public async Task SignUp_ShouldReturnTheCodeVerifier_GivenEmailAndThePkceFlow()
    {
        var signedUp = await this.Client.SignUpAsync(Constants.SignUpType.Email, RandomEmail(), Password,
            new SignUpOptions { FlowType = Constants.OAuthFlowType.PKCE });
        signedUp.PKCEVerifier.Should().NotBeNullOrEmpty("the server accepted the code challenge, and the verifier is kept for the code exchange (spec auth.sign_in.sign_up, PKCE)");
    }

    [TestMethod]
    public async Task SignInAnonymously_ShouldCreateAnonymousSessionWithMetadata()
    {
        var session = await this.Client.SignInAnonymously(new SignInAnonymouslyOptions
        {
            Data = new Dictionary<string, object> { { "first_name", "John" } },
        });
        session.Should().NotBeNull();
        session!.User!.IsAnonymous.Should().BeTrue();
        session.User.UserMetadata["first_name"].Should().Be("John");
    }
}
