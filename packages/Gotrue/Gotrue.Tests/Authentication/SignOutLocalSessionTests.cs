#region

using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     Sign-out clears the local session even if the server call fails, and keeps it for the others scope.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignOutLocalSessionTests
{
    private IGotrueClient<User, Session> client = null!;
    private IGotrueSessionPersistence<Session> persistence = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
        this.persistence = SessionPersistenceSubstitute.Tracking();
        this.persistence.SaveSession(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        this.client.SetPersistence(this.persistence);
        this.client.LoadSession();
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignOut_ShouldClearTheLocalSession_GivenTheServerFails()
    {
        this.server.Given(Request.Create().WithPath("/logout").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));
        var signOut = () => this.client.SignOut();
        await signOut.Should().ThrowAsync<GotrueException>();
        using (new AssertionScope())
        {
            this.client.CurrentSession.Should().BeNull("a failed sign-out must not leave the user signed in");
            this.persistence.LoadSession().Should().BeNull();
        }
    }

    [TestMethod]
    public async Task SignOut_ShouldKeepTheLocalSession_GivenOthersScope()
    {
        this.server.Given(Request.Create().WithPath("/logout").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(204));
        await this.client.SignOut(SignOutScope.Others);
        this.client.CurrentSession.Should().NotBeNull("the others scope signs out every session except this one");
    }
}
