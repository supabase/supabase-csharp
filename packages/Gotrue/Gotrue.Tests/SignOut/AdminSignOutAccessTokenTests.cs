#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.SignOut;

/// <summary>
///     The access-token check made before an admin sign-out leaves the SDK.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class AdminSignOutAccessTokenTests
{
    private static readonly AdminClient Admin = new("service-role-jwt", new ClientOptions { Url = "http://localhost" });

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task SignOutAsync_ShouldThrowArgument_GivenBlankAccessToken(string? accessToken)
    {
        var signOut = () => Admin.SignOutAsync(accessToken!);
        await signOut.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage("*access token is required*", "without a token the auth server can only answer 401 no_authorization")
            .WithParameterName("accessToken");
    }
}
