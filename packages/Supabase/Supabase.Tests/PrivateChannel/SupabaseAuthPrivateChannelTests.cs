using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Supabase.Gotrue.Constants;

namespace Supabase.Tests.PrivateChannel;

/// <summary>
/// A Supabase client signed in through Supabase Auth, against a live Supabase CLI stack (<c>supabase start</c>,
/// localhost:54321): the client hands the session token to Realtime, so <c>Realtime.PrivateChannel</c> joins the
/// user's own RLS-protected <c>user:&lt;uid&gt;</c> topic without the caller wiring a token
/// (realtime.subscriptions.private_channel).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SupabaseAuthPrivateChannelTests
{
    private const string PublishableKey = "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH";

    [TestMethod]
    public async Task PrivateChannel_ShouldJoinTheUsersOwnTopic_GivenASignedInUser()
    {
        var client = new Client("http://127.0.0.1:54321", PublishableKey, new SupabaseOptions());
        var signedUp = await client.Auth.SignUpAsync(SignUpType.Email, $"{Guid.NewGuid():N}@example.com", "a-strong-password-1");
        await client.Realtime.ConnectAsync();
        var channel = client.Realtime.PrivateChannel($"user:{signedUp.User!.Id}");
        await channel.Subscribe();
        channel.IsJoined.Should().BeTrue("the sign-in pushed the session token to Realtime, and the topic's RLS policy admits that user");
        client.Realtime.Disconnect();
    }
}
