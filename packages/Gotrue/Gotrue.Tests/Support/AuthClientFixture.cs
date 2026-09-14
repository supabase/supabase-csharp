#region

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     Base fixture for E2E tests that drive the stateful <c>Client</c> against the live stack. It provides a
///     fresh client per test wired to a tracking persistence double and an auth-state recorder, plus the
///     shared "what a signed-in / signed-out client looks like" assertions. Derived classes add only the
///     scenario under test.
/// </summary>
public abstract class AuthClientFixture
{
    protected readonly List<Constants.AuthState> StateChanges = new();
    protected IGotrueClient<User, Session> Client { get; private set; } = null!;
    protected IGotrueSessionPersistence<Session> Persistence { get; private set; } = null!;

    [TestInitialize]
    public void InitializeClient()
    {
        this.StateChanges.Clear();
        this.Persistence = SessionPersistenceSubstitute.Tracking();
        this.Client = TestClients.AgainstCliStack();
        this.Client.SetPersistence(this.Persistence);
        this.Client.AddStateChangedListener((_, state) => this.StateChanges.Add(state));
    }

    protected async Task<Session> SignUpNewUser()
    {
        var session = await this.Client.SignUp(RandomEmail(), Password);
        session.Should().NotBeNull();
        return session!;
    }

    protected void VerifyGoodSession(Session? session)
    {
        using (new AssertionScope())
        {
            session.Should().NotBeNull();
            this.StateChanges.Should().Contain(SignedIn);
            this.Persistence.LoadSession().Should().BeSameAs(this.Client.CurrentSession, "the SDK persists the session it signed in");
            this.Client.CurrentUser!.Id.Should().Be(session!.User!.Id);
            session.AccessToken.Should().NotBeNull();
            session.RefreshToken.Should().NotBeNull();
            session.User.Should().NotBeNull();
        }
    }

    protected void VerifySignedOut()
    {
        using (new AssertionScope())
        {
            this.StateChanges.Should().ContainSingle(state => state == SignedOut);
            this.Persistence.LoadSession().Should().BeNull();
            this.Client.CurrentSession.Should().BeNull();
            this.Client.CurrentUser.Should().BeNull();
        }
    }
}
