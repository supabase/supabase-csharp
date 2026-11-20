#region

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateOAuthClient;

#endregion

namespace Gotrue.Tests.CreateOAuthClient;

/// <summary>
///     Building an OAuth client to register: the settings every client needs, the client type and authentication method
///     pairing each factory guarantees, and keeping the client secret out of logs.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class NewOAuthClientTests
{
    private static readonly Uri[] Redirects = { new("https://app.example.com/callback") };

    [TestMethod]
    public void Confidential_ShouldUseClientSecretBasic_GivenNoAuthMethod() =>
        NewOAuthClient.Confidential("My App", Redirects).Should().BeEquivalentTo(new
        {
            Type = OAuthClientType.Confidential,
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretBasic,
        }, "RFC 7591 defaults the token endpoint authentication method to client_secret_basic");

    [TestMethod]
    public void Confidential_ShouldKeepClientSecretPost_GivenIt() =>
        NewOAuthClient.Confidential("My App", Redirects, OAuthClientTokenEndpointAuthMethod.ClientSecretPost)
            .TokenEndpointAuthMethod.Should().Be(OAuthClientTokenEndpointAuthMethod.ClientSecretPost);

    [TestMethod]
    public void Confidential_ShouldThrowArgument_GivenNoneAuthMethod()
    {
        var build = () => NewOAuthClient.Confidential("My App", Redirects, OAuthClientTokenEndpointAuthMethod.None);
        build.Should().ThrowExactly<ArgumentException>().WithMessage("*use ClientSecretBasic or ClientSecretPost*").And.ParamName.Should().Be("authMethod",
            "the auth server rejects a confidential client whose token endpoint authentication method is none");
    }

    [TestMethod]
    public void Public_ShouldUseNoneAuthMethod() =>
        NewOAuthClient.Public("My App", Redirects).Should().BeEquivalentTo(new
        {
            Type = OAuthClientType.Public,
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.None,
        }, "a public client has no secret to authenticate with");

    [TestMethod]
    public void Factory_ShouldKeepNameAndRedirectUris() =>
        NewOAuthClient.Public("My App", Redirects).Should().BeEquivalentTo(new { Name = "My App", RedirectUris = Redirects });

    [TestMethod]
    public void Factory_ShouldThrowArgumentNull_GivenNullName()
    {
        var build = () => NewOAuthClient.Public(null!, Redirects);
        build.Should().ThrowExactly<ArgumentNullException>().WithParameterName("name");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void Factory_ShouldThrowArgument_GivenBlankName(string name)
    {
        var build = () => NewOAuthClient.Public(name, Redirects);
        build.Should().ThrowExactly<ArgumentException>().WithMessage("*needs a name*").And.ParamName.Should().Be("name");
    }

    [TestMethod]
    public void Factory_ShouldThrowArgumentNull_GivenNullRedirectUris()
    {
        var build = () => NewOAuthClient.Public("My App", null!);
        build.Should().ThrowExactly<ArgumentNullException>().WithParameterName("redirectUris");
    }

    [TestMethod]
    public void Factory_ShouldThrowArgument_GivenNoRedirectUris()
    {
        var build = () => NewOAuthClient.Public("My App", Array.Empty<Uri>());
        build.Should().ThrowExactly<ArgumentException>().WithMessage("*at least one redirect URI*").And.ParamName.Should().Be("redirectUris",
            "the auth server requires at least one redirect URI");
    }
}
