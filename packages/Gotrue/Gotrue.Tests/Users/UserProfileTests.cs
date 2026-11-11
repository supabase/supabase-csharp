#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.Users;

/// <summary>
///     End-to-end reads and edits of the signed-in user against the live stack: updating user metadata and
///     retrieving the user by access token.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class UserProfileTests : AuthClientFixture
{
    [TestMethod]
    public async Task Update_ShouldPersistUserMetadataAndRaiseUserUpdated()
    {
        var email = RandomEmail();
        await this.Client.SignUpAsync(Constants.SignUpType.Email, email, Password);
        this.StateChanges.Clear();
        var updated = await this.Client.Update(new UserAttributes
        {
            Data = new Dictionary<string, object> { { "hello", "world" } },
        });
        updated.Should().NotBeNull();
        this.Client.CurrentUser!.Email.Should().Be(email);
        this.Client.CurrentUser.UserMetadata.Should().ContainKey("hello");
        this.StateChanges.Should().Contain(UserUpdated);
        this.Persistence.LoadSession().Should().BeSameAs(this.Client.CurrentSession);
    }

    [TestMethod]
    public async Task GetUser_ShouldReturnTheUser_GivenAccessToken()
    {
        var email = RandomEmail();
        var session = (await this.Client.SignUpAsync(Constants.SignUpType.Email, email, Password)).Session;
        this.Client.CurrentUser!.Email.Should().Be(email);
        var byToken = (await this.Client.GetUser(session!.AccessToken ?? throw new InvalidOperationException()))!;
        byToken.Email.Should().Be(email);
    }
}
