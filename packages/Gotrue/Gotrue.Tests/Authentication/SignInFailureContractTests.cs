#region

using System.Collections.Generic;
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
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     Pins what a rejected or invalid sign-in leaves behind: the session the user already had (issue #396).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignInFailureContractTests
{
    private readonly List<Constants.AuthState> stateChanges = new();
    private IGotrueClient<User, Session> client = null!;
    private IGotrueSessionPersistence<Session> persistence = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
        this.persistence = SessionPersistenceSubstitute.Tracking();
        this.persistence.SaveSession(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        this.client.SetPersistence(this.persistence);
        this.client.LoadSession();
        this.client.AddStateChangedListener((_, state) => this.stateChanges.Add(state));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignUp_ShouldKeepTheCurrentSession_GivenTheServerRejectsIt()
    {
        this.server.Given(Request.Create().WithPath("/signup").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(422)
                .WithHeader("Content-Type", "application/json").WithBody("""{"code":422,"error_code":"user_already_exists","msg":"User already registered"}"""));
        var signUp = () => this.client.SignUp(RandomEmail(), Password);
        await signUp.Should().ThrowAsync<GotrueException>();
        using (new AssertionScope())
        {
            this.client.CurrentSession!.RefreshToken.Should().Be("a-refresh-token", "a rejected sign-up must not sign the user out (issue #396)");
            this.persistence.LoadSession().Should().NotBeNull();
            this.stateChanges.Should().BeEmpty();
        }
    }

    [TestMethod]
    public async Task SetSession_ShouldKeepTheCurrentSession_GivenEmptyTokens()
    {
        var setSession = () => this.client.SetSession("", "");
        await setSession.Should().ThrowAsync<GotrueException>();
        using (new AssertionScope())
        {
            this.client.CurrentSession!.RefreshToken.Should().Be("a-refresh-token");
            this.stateChanges.Should().BeEmpty();
        }
    }
}
