using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;

namespace Supabase.Tests.DisabledSessionPersistence;

/// <summary>
/// A client configured with <see cref="SessionPersistence.Disabled"/> keeps its session in memory for
/// its own lifetime only, against the live stack (<c>supabase start</c>, localhost:54321).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class DisabledSessionPersistenceTests
{
    private const string AnonKey =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";

    private static SupabaseOptions DisabledPersistenceOptions() => new()
    {
        SessionHandler = SessionPersistence.Disabled,
        AuthUrlFormat = "{0}:54321/auth/v1",
        AutoConnectRealtime = false,
    };

    private static async Task<Supabase.Client> InitializedClient()
    {
        var client = new Supabase.Client("http://localhost", AnonKey, DisabledPersistenceOptions());
        await client.InitializeAsync();
        return client;
    }

    [TestMethod]
    public async Task SignUp_ShouldKeepSessionInMemoryOnly_GivenDisabledSessionPersistence()
    {
        var signedUp = await InitializedClient();
        await signedUp.Auth.SignUpAsync(SignUpType.Email, $"{Guid.NewGuid():N}@example.com", "P@ssw0rd-1234");
        var restarted = await InitializedClient();
        using (new AssertionScope())
        {
            signedUp.Auth.CurrentSession.Should().NotBeNull("the session stays available for the client's own lifetime");
            restarted.Auth.CurrentSession.Should().BeNull("nothing was persisted for another client to restore");
        }
    }
}
