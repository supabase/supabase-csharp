#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace Gotrue.Tests.Identities;

/// <summary>
///     End-to-end listing of the identities linked to the signed-in user against the live stack.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class UserIdentitiesTests : AuthClientFixture
{
    [TestMethod]
    public async Task GetUserIdentitiesAsync_ShouldReturnTheEmailIdentity_GivenUserSignedUpWithEmail()
    {
        var session = await this.SignUpNewUser();
        var identities = await this.Client.GetUserIdentitiesAsync();
        identities.Should().ContainSingle(identity => identity.Provider == "email" && identity.UserId == session.User!.Id,
            "GoTrue creates an email identity for an email/password sign-up (GET /user identities)");
    }
}
