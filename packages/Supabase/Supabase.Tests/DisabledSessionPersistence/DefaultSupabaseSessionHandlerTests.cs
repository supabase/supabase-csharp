using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Supabase.Tests.DisabledSessionPersistence;

/// <summary>
/// <c>DefaultSupabaseSessionHandler</c> is obsolete in favor of <see cref="SessionPersistence.Disabled"/> and
/// is removed in 9.0.0; until then it keeps persisting nothing. The tests reach it by reflection so the SDK
/// itself never references an obsolete type.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DefaultSupabaseSessionHandlerTests
{
    private static readonly Type HandlerType = typeof(Supabase.Client).Assembly.GetType("Supabase.DefaultSupabaseSessionHandler")!;

    private static IGotrueSessionPersistence<Session> NewHandler() =>
        (IGotrueSessionPersistence<Session>)Activator.CreateInstance(HandlerType)!;

    [TestMethod]
    public void DefaultSupabaseSessionHandler_ShouldPointToSessionPersistenceDisabled_GivenItIsObsolete() =>
        HandlerType.GetCustomAttribute<ObsoleteAttribute>()!.Message
            .Should().Contain("SessionPersistence.Disabled", "callers need the replacement named at the warning");

    [TestMethod]
    public async Task LoadSession_ShouldReturnNull_GivenASavedSession()
    {
        var handler = NewHandler();
        handler.SaveSession(new Session { AccessToken = "an-access-token" });
        handler.DestroySession();
        using (new AssertionScope())
        {
            handler.LoadSession().Should().BeNull("the handler persists nothing");
            (await handler.LoadSessionAsync()).Should().BeNull("the async load wraps the sync one");
        }
    }
}
