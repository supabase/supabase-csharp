#region

using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     Builds an <see cref="IGotrueSessionPersistence{Session}" /> substitute that behaves like an in-memory
///     store: whatever the SDK saves is returned on the next load, and destroying clears it. Both the
///     synchronous and asynchronous members are wired to the same backing slot, so a test can seed or read
///     it through either contract. Tests observe the persisted session through <c>LoadSession()</c> /
///     <c>LoadSessionAsync()</c> (its public contract) rather than any test-only field, keeping the double
///     honest and free of hand-written stub classes (CONVENTIONS §5.6).
/// </summary>
internal static class SessionPersistenceSubstitute
{
    internal static IGotrueSessionPersistence<Session> Tracking()
    {
        var persistence = Substitute.For<IGotrueSessionPersistence<Session>>();
        Session? saved = null;
        persistence.When(p => p.SaveSession(Arg.Any<Session>())).Do(call => saved = call.Arg<Session>());
        persistence.When(p => p.DestroySession()).Do(_ => saved = null);
        persistence.LoadSession().Returns(_ => saved);
        persistence.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saved = call.Arg<Session>();
                return Task.CompletedTask;
            });
        persistence.DestroySessionAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                saved = null;
                return Task.CompletedTask;
            });
        persistence.LoadSessionAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(saved));
        return persistence;
    }

    /// <summary>
    ///     An async-only store, the canonical shape for a backend that can only be reached asynchronously
    ///     (e.g. Blazor WASM local storage over JS interop): the synchronous members throw, so any test using
    ///     it proves the SDK drove the write purely through the awaited async path and never blocked on the
    ///     sync members.
    /// </summary>
    internal static IGotrueSessionPersistence<Session> AsyncOnly()
    {
        var persistence = Substitute.For<IGotrueSessionPersistence<Session>>();
        Session? saved = null;
        persistence.When(p => p.SaveSession(Arg.Any<Session>()))
            .Do(_ => throw new NotSupportedException("An async-only store must not receive a synchronous SaveSession."));
        persistence.When(p => p.DestroySession())
            .Do(_ => throw new NotSupportedException("An async-only store must not receive a synchronous DestroySession."));
        persistence.LoadSession()
            .Returns(_ => throw new NotSupportedException("An async-only store must not receive a synchronous LoadSession."));
        persistence.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saved = call.Arg<Session>();
                return Task.CompletedTask;
            });
        persistence.DestroySessionAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                saved = null;
                return Task.CompletedTask;
            });
        persistence.LoadSessionAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(saved));
        return persistence;
    }
}
