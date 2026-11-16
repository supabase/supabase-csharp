using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Core.Http;
using Supabase.Functions.Interfaces;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Storage;
using Supabase.Storage.Interfaces;
using Supabase.Tests.Support;

namespace Supabase.Tests.ThirdPartyAuth;

/// <summary>
/// How the composed <see cref="Supabase.Client"/> wires <see cref="SupabaseOptions.ThirdPartyAccessToken"/>: no Supabase
/// Auth client, the callback handed to every sub-client that sends requests, and the caller's options left untouched
/// (client.authentication_integration.third_party_auth). Live round-trips are in <see cref="ThirdPartyAuthTests"/>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ThirdPartyAuthCompositionTests
{
    private const string ApiKey = "test-key";
    private const string ThirdPartyJwt = "a-third-party-jwt";
    private static readonly Func<CancellationToken, Task<string?>> SignedIn = _ => Task.FromResult<string?>(ThirdPartyJwt);
    private static readonly Func<CancellationToken, Task<string?>> SignedOut = _ => Task.FromResult<string?>(null);

    private static Supabase.Client ClientWith(Func<CancellationToken, Task<string?>>? thirdPartyAccessToken, SupabaseOptions? options = null)
    {
        var configured = options ?? new SupabaseOptions();
        configured.ThirdPartyAccessToken = thirdPartyAccessToken;
        return new Supabase.Client("http://localhost", ApiKey, configured);
    }

    [TestMethod]
    public void Auth_ShouldThrow_GivenAThirdPartyAccessToken()
    {
        var reading = () => ClientWith(SignedIn).Auth;
        reading.Should().Throw<InvalidOperationException>("the client builds no Supabase Auth client when a third-party provider owns sign-in")
            .WithMessage("*ThirdPartyAccessToken*");
    }

    [TestMethod]
    public void Auth_ShouldRejectAReplacement_GivenAThirdPartyAccessToken()
    {
        var client = ClientWith(SignedIn);
        var replacing = () => client.Auth = new Gotrue.Client();
        replacing.Should().Throw<InvalidOperationException>("a Supabase Auth session would compete with the third-party token for the Authorization header");
    }

    [TestMethod]
    public void AdminAuth_ShouldThrow_GivenAThirdPartyAccessToken()
    {
        var creating = () => ClientWith(SignedIn).AdminAuth("a-service-key");
        creating.Should().Throw<InvalidOperationException>("the admin API belongs to Supabase Auth, which the client does not use with a third-party provider");
    }

    [TestMethod]
    public void AdminAuth_ShouldCreateAnAdminClient_GivenNoThirdPartyAccessToken() =>
        ClientWith(null).AdminAuth("a-service-key").Should().NotBeNull();

    [TestMethod]
    [DataRow("Authorization")]
    [DataRow("authorization")]
    public void Client_ShouldThrow_GivenAThirdPartyAccessTokenAndAnAuthorizationHeader(string header)
    {
        var options = new SupabaseOptions { Headers = new Dictionary<string, string> { { header, "Bearer a-fixed-token" } } };
        var creating = () => ClientWith(SignedIn, options);
        creating.Should().Throw<ArgumentException>("two sources for the caller's identity leave no correct choice, so the conflict is rejected before any request")
            .WithMessage("*ThirdPartyAccessToken*Headers*");
    }

    [TestMethod]
    public void Client_ShouldKeepTheAuthorizationHeader_GivenNoThirdPartyAccessToken()
    {
        var options = new SupabaseOptions { Headers = new Dictionary<string, string> { { "Authorization", "Bearer a-fixed-token" } } };
        ClientWith(null, options).Postgrest.GetHeaders!()["Authorization"].Should().Be("Bearer a-fixed-token");
    }

    [TestMethod]
    public void Client_ShouldHandTheCallbackToPostgrestAndFunctions_GivenAThirdPartyAccessToken()
    {
        var client = ClientWith(SignedIn);
        using (new AssertionScope())
        {
            client.Postgrest.Options.RetrieveAccessToken.Should().BeSameAs(SignedIn);
            ((Supabase.Functions.Client) client.Functions).Options.RetrieveAccessToken.Should().BeSameAs(SignedIn);
        }
    }

    [TestMethod]
    public void Client_ShouldSendTheApiKeyAsTheFallbackBearer_GivenAThirdPartyAccessToken() =>
        ClientWith(SignedIn).Postgrest.GetHeaders!()["Authorization"].Should().Be($"Bearer {ApiKey}",
            "a null third-party token sends the request with the project API key, as when no user is signed in");

    [TestMethod]
    public void Client_ShouldHandTheCallbackToStorageWithoutChangingTheCallerOptions_GivenAThirdPartyAccessToken()
    {
        var storageOptions = DistinctStorageOptions();
        var client = ClientWith(SignedIn, new SupabaseOptions { StorageClientOptions = storageOptions });
        using (new AssertionScope())
        {
            client.Storage.Options.RetrieveAccessToken.Should().BeSameAs(SignedIn);
            client.Storage.Options.Should().BeEquivalentTo(storageOptions, o => o.Excluding(options => options.RetrieveAccessToken),
                "every other Storage setting the caller chose still applies");
            storageOptions.RetrieveAccessToken.Should().BeNull("the caller's options object is not modified");
        }
    }

    [TestMethod]
    public void Client_ShouldUseTheCallerStorageOptions_GivenNoThirdPartyAccessToken()
    {
        var storageOptions = new Storage.ClientOptions();
        ClientWith(null, new SupabaseOptions { StorageClientOptions = storageOptions }).Storage.Options.Should().BeSameAs(storageOptions);
    }

    [TestMethod]
    public async Task Realtime_ShouldUseTheThirdPartyToken_GivenTheCallbackReturnsOne() =>
        (await ClientWith(SignedIn).Realtime.Options.RetrieveAccessToken!()).Should().Be(ThirdPartyJwt);

    [TestMethod]
    public async Task Realtime_ShouldUseTheApiKey_GivenTheCallbackReturnsNull() =>
        (await ClientWith(SignedOut).Realtime.Options.RetrieveAccessToken!()).Should().Be(ApiKey,
            "a null third-party token connects Realtime with the project API key, as after a sign-out");

    [TestMethod]
    public void Realtime_ShouldHaveNoAccessTokenCallback_GivenNoThirdPartyAccessToken() =>
        ClientWith(null).Realtime.Options.RetrieveAccessToken.Should().BeNull("Supabase Auth pushes its session to Realtime through SetAuth instead");

    [TestMethod]
    public void Realtime_ShouldResolveTheToken_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        var realtime = ClientWith(_ => Task.Run<string?>(() => ThirdPartyJwt)).Realtime;
        BlockingCaller.Completes(() => realtime.Options.RetrieveAccessToken!()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public void Auth_ShouldPushTheApiKeyToRealtime_GivenASignOut()
    {
        var realtime = Substitute.For<IRealtimeClient<RealtimeSocket, RealtimeChannel>>();
        realtime.Subscriptions.Returns(new ReadOnlyDictionary<string, RealtimeChannel>(new Dictionary<string, RealtimeChannel>()));
        var client = ClientWith(null);
        client.Realtime = realtime;
        client.Auth.NotifyAuthStateChange(Gotrue.Constants.AuthState.SignedOut);
        realtime.Received().SetAuth(ApiKey);
    }

    [TestMethod]
    public void InitializeAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations() =>
        BlockingCaller.Completes(() => ClientOver(AuthWithSession(null)).InitializeAsync()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");

    [TestMethod]
    public void InitializeAsync_ShouldComplete_GivenAnExistingSessionAndCallerBlockingOnContextThatNeverRunsContinuations() =>
        BlockingCaller.Completes(() => ClientOver(AuthWithSession(new Session())).InitializeAsync()).Should().BeTrue(
            "refreshing a session the caller already holds must not resume on the caller's context either");

    private static IGotrueClient<User, Session> AuthWithSession(Session? session)
    {
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        auth.CurrentSession.Returns(session);
        auth.LoadSessionAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.Run(() => { }));
        auth.RetrieveSessionAsync().Returns(_ => Task.Run(() => (Session?) null));
        return auth;
    }

    private static Supabase.Client ClientOver(IGotrueClient<User, Session> auth)
    {
        var realtime = Substitute.For<IRealtimeClient<RealtimeSocket, RealtimeChannel>>();
        realtime.Options.Returns(new Realtime.ClientOptions());
        return new Supabase.Client(auth, realtime, Substitute.For<IFunctionsClient>(), Substitute.For<IPostgrestClient>(),
            Substitute.For<IStorageClient<Bucket, FileObject>>(), new SupabaseOptions());
    }

    [TestMethod]
    public async Task InitializeAsync_ShouldComplete_GivenAThirdPartyAccessToken()
    {
        var client = ClientWith(SignedIn);
        (await client.InitializeAsync()).Should().BeSameAs(client, "there is no Supabase Auth session to restore");
    }

    private static Storage.ClientOptions DistinctStorageOptions() =>
        new()
        {
            HttpUploadTimeout = TimeSpan.FromSeconds(1),
            HttpDownloadTimeout = TimeSpan.FromSeconds(2),
            HttpRequestTimeout = TimeSpan.FromSeconds(3),
            HttpRequestClient = new HttpClient(),
            HttpUploadClient = new HttpClient(),
            HttpDownloadClient = new HttpClient(),
            Proxy = new WebProxy("http://proxy.example.com"),
            Retry = new RetryOptions { MaxRetries = 4 },
        };
}
