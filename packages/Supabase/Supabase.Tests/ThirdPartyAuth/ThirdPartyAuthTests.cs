using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Functions.Exceptions;
using Supabase.Postgrest.Exceptions;
using Supabase.Tests.Models;

namespace Supabase.Tests.ThirdPartyAuth;

/// <summary>
/// A Supabase client authenticated by <see cref="SupabaseOptions.ThirdPartyAccessToken"/> against a live Supabase CLI
/// stack (<c>supabase start</c>, localhost:54321). The JWT of a user signed up through a plain HTTP call stands in for
/// an external provider's token, since the stack trusts it: every sub-client sends it, a malformed one is rejected by
/// the server, and a throwing callback fails the request (client.authentication_integration.third_party_auth).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ThirdPartyAuthTests
{
    private const string StackUrl = "http://127.0.0.1:54321";
    private const string PublishableKey = "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH";
    private const string MalformedJwt = "not-a-jwt";
    private const string AuthenticatedFolderFile = "casestudy.png";

    [TestMethod]
    public async Task Storage_ShouldListTheAuthenticatedFolder_GivenAUserJwt()
    {
        var user = await SignUpUserAsync();
        var files = await ClientReturning(user.AccessToken).Storage.From("bucket2").List("authenticated");
        files!.Select(file => file.Name).Should().Contain(AuthenticatedFolderFile,
            "the folder's RLS policy admits the authenticated role, which only the third-party JWT carries");
    }

    [TestMethod]
    public async Task Storage_ShouldHideTheAuthenticatedFolder_GivenTheCallbackReturnsNull()
    {
        var files = await ClientReturning(null).Storage.From("bucket2").List("authenticated");
        files.Should().BeNullOrEmpty("a null token sends the request with the API key, which RLS keeps out of the folder");
    }

    [TestMethod]
    public async Task Realtime_ShouldJoinTheUsersPrivateChannel_GivenAUserJwt()
    {
        var user = await SignUpUserAsync();
        var realtime = ClientReturning(user.AccessToken).Realtime;
        await realtime.ConnectAsync();
        var channel = realtime.PrivateChannel($"user:{user.Id}");
        await channel.Subscribe();
        channel.IsJoined.Should().BeTrue("the private topic's RLS policy admits only the user whose JWT Realtime received on connect");
        realtime.Disconnect();
    }

    [TestMethod]
    public async Task Postgrest_ShouldBeRejected_GivenAMalformedJwt()
    {
        var querying = () => ClientReturning(MalformedJwt).From<Channel>().Get();
        (await querying.Should().ThrowAsync<PostgrestException>()).Which.StatusCode.Should().Be((int) HttpStatusCode.Unauthorized,
            "the server received the callback's token, not the API key");
    }

    [TestMethod]
    public async Task Functions_ShouldBeRejected_GivenAMalformedJwt()
    {
        var invoking = () => ClientReturning(MalformedJwt).Functions.Invoke("hello");
        (await invoking.Should().ThrowAsync<FunctionsException>()).Which.StatusCode.Should().Be((int) HttpStatusCode.Unauthorized,
            "the server received the callback's token, not the API key");
    }

    [TestMethod]
    public async Task Postgrest_ShouldThrowTheCallbackFailure_GivenTheCallbackThrows()
    {
        var failure = new InvalidOperationException("the provider could not resolve the user");
        var client = new Client(StackUrl, PublishableKey, new SupabaseOptions { ThirdPartyAccessToken = _ => throw failure });
        var querying = () => client.From<Channel>().Get();
        (await querying.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure,
            "an identity that cannot be resolved fails the request instead of falling back to the API key");
    }

    private static Client ClientReturning(string? accessToken) =>
        new(StackUrl, PublishableKey, new SupabaseOptions { ThirdPartyAccessToken = _ => Task.FromResult(accessToken) });

    private static async Task<SignedUpUser> SignUpUserAsync()
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{StackUrl}/auth/v1/signup")
        {
            Content = JsonContent.Create(new { email = $"{Guid.NewGuid():N}@example.com", password = "a-strong-password-1" }),
        };
        request.Headers.Add("apikey", PublishableKey);
        using var response = await http.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
        var session = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return new SignedUpUser((string) session["user"]!["id"]!, (string) session["access_token"]!);
    }

    private sealed record SignedUpUser(string Id, string AccessToken);
}
