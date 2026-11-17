#region

using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.DisabledSessionPersistence;

/// <summary>
///     <see cref="SessionPersistence.Disabled" /> keeps the session in memory only: it stores nothing, restores
///     nothing, and replaces any store a client was given before.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DisabledSessionPersistenceTests
{
    private static readonly Session AnySession =
        new() { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 };

    private static Client NewClient() =>
        new(new ClientOptions { Url = "http://localhost", AutoRefreshToken = false });

    [TestMethod]
    public void Disabled_ShouldBeTheSameInstance_GivenSeveralReads() =>
        SessionPersistence.Disabled.Should().BeSameAs(SessionPersistence.Disabled);

    [TestMethod]
    public void LoadSession_ShouldReturnNull_GivenASavedSession()
    {
        SessionPersistence.Disabled.SaveSession(AnySession);
        SessionPersistence.Disabled.LoadSession().Should().BeNull("nothing is ever stored");
    }

    [TestMethod]
    public async Task LoadSessionAsync_ShouldReturnNull_GivenASavedSession()
    {
        await SessionPersistence.Disabled.SaveSessionAsync(AnySession);
        (await SessionPersistence.Disabled.LoadSessionAsync()).Should().BeNull("nothing is ever stored");
    }

    [TestMethod]
    public async Task DestroySession_ShouldComplete_GivenNoStoredSession()
    {
        SessionPersistence.Disabled.DestroySession();
        await SessionPersistence.Disabled.DestroySessionAsync();
    }

    [TestMethod]
    public void LoadSession_ShouldLeaveClientSignedOut_GivenDisabledPersistence()
    {
        var client = NewClient();
        client.SetPersistence(SessionPersistence.Disabled);
        client.LoadSession();
        client.CurrentSession.Should().BeNull("there is no persisted session to restore");
    }

    [TestMethod]
    public void LoadSession_ShouldLeaveClientSignedOut_GivenDisabledPersistenceReplacedAStore()
    {
        var store = SessionPersistenceSubstitute.Tracking();
        store.SaveSession(AnySession);
        var client = NewClient();
        client.SetPersistence(store);
        client.SetPersistence(SessionPersistence.Disabled);
        client.LoadSession();
        using (new AssertionScope())
        {
            client.CurrentSession.Should().BeNull("the replaced store is no longer read");
            store.LoadSession().Should().BeSameAs(AnySession, "the replaced store is left untouched");
        }
    }
}
