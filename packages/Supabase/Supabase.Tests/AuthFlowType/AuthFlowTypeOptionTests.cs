using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Supabase.Gotrue.Constants;
using static Supabase.StatelessClient;

namespace Supabase.Tests.AuthFlowType;

/// <summary>
/// <see cref="SupabaseOptions.AuthFlowType"/> reaches Auth: the composed client and the stateless facade both hand it
/// to Gotrue as the client's default flow type (client.authentication_integration.oauth_flow_type).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class AuthFlowTypeOptionTests
{
    private const string SupabaseUrl = "http://localhost";
    private const string PublishableKey = "sb_publishable_abc123";
    private static readonly SupabaseOptions PkceOptions = new() { AuthFlowType = OAuthFlowType.PKCE };

    [TestMethod]
    public async Task Auth_ShouldStartAPkceFlow_GivenAPkceAuthFlowType()
    {
        var state = await new Client(SupabaseUrl, PublishableKey, PkceOptions).Auth.SignIn(Provider.Github);
        state.PKCEVerifier.Should().NotBeNullOrEmpty("the umbrella passes AuthFlowType to Auth as its default flow type");
    }

    [TestMethod]
    public void GetAuthOptions_ShouldCarryTheAuthFlowType_GivenAPkceAuthFlowType() =>
        GetAuthOptions(SupabaseUrl, PublishableKey, PkceOptions).FlowType.Should().Be(OAuthFlowType.PKCE,
            "a Gotrue client built from the stateless options keeps the configured default flow type");
}
