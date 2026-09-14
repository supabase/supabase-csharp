using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;

namespace Realtime.Tests.Channels;

/// <summary>
///     Untrusted-client authorization against the live stack (supabase-csharp#400): a publishable/anon socket
///     plus a signed-in user's JWT joining a private, RLS-protected per-user topic. This is the scenario the
///     service-role broadcast tests cannot cover, because a service role bypasses RLS. The join is authorized
///     during <c>phx_join</c>, so it only succeeds when the user's JWT rides the join frame. The migration
///     <c>1751173720_realtime_fixtures.sql</c> defines the matching <c>realtime.messages</c> policy.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class PrivateChannelAuthorizationTests
{
    // The local CLI stack's demo JWT secret, shared with the other SDK test suites.
    private const string JwtSecret = "super-secret-jwt-token-with-at-least-32-characters-long";

    [TestMethod]
    public async Task Subscribe_ShouldJoinPrivateChannel_GivenAuthenticatedUserOnOwnTopic()
    {
        var userId = Guid.NewGuid().ToString();
        var channel = await this.SubscribePrivate(topicUserId: userId, jwtUserId: userId);

        Assert.IsTrue(channel.IsJoined,
            "the RLS policy authorizes 'user:<own-uid>', so the private join must succeed once the JWT rides the join frame (#400)");
    }

    [TestMethod]
    public async Task Subscribe_ShouldReject_GivenAuthenticatedUserOnAnotherUsersTopic()
    {
        var otherUserId = Guid.NewGuid().ToString();
        var ownUserId = Guid.NewGuid().ToString();

        await Assert.ThrowsAsync<RealtimeException>(() => this.SubscribePrivate(topicUserId: otherUserId, jwtUserId: ownUserId),
            "the JWT authorizes only the user's own topic, so joining another user's topic must be denied by RLS");
    }

    private async Task<Supabase.Realtime.RealtimeChannel> SubscribePrivate(string topicUserId, string jwtUserId)
    {
        var client = Helpers.SocketClient();
        await client.ConnectAsync();
        var jwt = MintUserJwt(jwtUserId);
        var channel = client.Channel($"user:{topicUserId}",
            ChannelOptions.Private(client.Options, () => jwt, client.SerializerSettings));
        channel.Register<BroadcastExample>();
        return (Supabase.Realtime.RealtimeChannel) await channel.Subscribe();
    }

    /// <summary>Signs a minimal Supabase 'authenticated' JWT (HS256) whose <c>sub</c> becomes <c>auth.uid()</c>.</summary>
    private static string MintUserJwt(string userId)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes(
            $"{{\"iss\":\"supabase-demo\",\"role\":\"authenticated\",\"aud\":\"authenticated\",\"sub\":\"{userId}\",\"iat\":1751000000,\"exp\":1983812996}}"));
        var signingInput = $"{header}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(JwtSecret));
        var signature = Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));
        return $"{signingInput}.{signature}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
