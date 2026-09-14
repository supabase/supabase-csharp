using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Functions.Interfaces;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Storage;
using Supabase.Storage.Interfaces;
using static Supabase.Gotrue.Constants;

namespace Supabase.Tests;

/// <summary>
/// The umbrella <see cref="Supabase.Client"/> owns no transport of its own — every request belongs to a
/// child package with its own tests. What it uniquely owns, and what this fixture pins, is how it
/// <em>composes</em> those children: constructing them, wiring Realtime to Postgrest, propagating the
/// auth headers, and forwarding auth-state changes. All hermetic; live round-trips are in
/// <see cref="SupabaseClientDatabaseTests"/>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SupabaseClientCompositionTests
{
    private static Supabase.Client UrlClient(SupabaseOptions? options = null) =>
        new("http://localhost", "test-key", options ?? new SupabaseOptions { AutoConnectRealtime = false });

    // Builds the umbrella through its DI constructor with substituted children; a test overrides only
    // the collaborator it cares about.
    private static Supabase.Client DiClient(
        IGotrueClient<User, Session>? auth = null,
        IRealtimeClient<RealtimeSocket, RealtimeChannel>? realtime = null,
        IPostgrestClient? postgrest = null,
        SupabaseOptions? options = null) =>
        new(auth ?? Substitute.For<IGotrueClient<User, Session>>(),
            realtime ?? RealtimeSubstitute(),
            Substitute.For<IFunctionsClient>(),
            postgrest ?? Substitute.For<IPostgrestClient>(),
            Substitute.For<IStorageClient<Bucket, FileObject>>(),
            options ?? new SupabaseOptions());

    // The umbrella writes Options.PostgrestClient during construction, so the substitute needs real Options.
    private static IRealtimeClient<RealtimeSocket, RealtimeChannel> RealtimeSubstitute()
    {
        var realtime = Substitute.For<IRealtimeClient<RealtimeSocket, RealtimeChannel>>();
        realtime.Options.Returns(new Supabase.Realtime.ClientOptions());
        return realtime;
    }

    [TestMethod]
    public void SupabaseClient_ShouldComposeAllChildClients_GivenUrlConstructor()
    {
        var client = UrlClient();
        using (new AssertionScope())
        {
            client.Auth.Should().NotBeNull();
            client.Realtime.Should().NotBeNull();
            client.Functions.Should().NotBeNull();
            client.Postgrest.Should().NotBeNull();
            client.Storage.Should().NotBeNull();
        }
    }

    [TestMethod]
    public void SupabaseClient_ShouldWireRealtimeToOwnPostgrestClient_GivenUrlConstructor()
    {
        var client = UrlClient();
        client.Realtime.Options.PostgrestClient.Should().BeSameAs(client.Postgrest,
            "postgres_changes models must reach the same Postgrest client the umbrella exposes (issue #35)");
    }

    [TestMethod]
    public void SupabaseClient_ShouldWireRealtimeToInjectedPostgrestClient_GivenDependencyInjectionConstructor()
    {
        var postgrest = Substitute.For<IPostgrestClient>();
        var realtime = RealtimeSubstitute();
        _ = DiClient(realtime: realtime, postgrest: postgrest);
        realtime.Options.PostgrestClient.Should().BeSameAs(postgrest);
    }

    [TestMethod]
    public void SupabaseClient_ShouldExposeInjectedChildren_GivenChildrenSetViaProperties()
    {
        var client = UrlClient();
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        var functions = Substitute.For<IFunctionsClient>();
        var realtime = Substitute.For<IRealtimeClient<RealtimeSocket, RealtimeChannel>>();
        var postgrest = Substitute.For<IPostgrestClient>();
        var storage = Substitute.For<IStorageClient<Bucket, FileObject>>();
        client.Auth = auth;
        client.Functions = functions;
        client.Realtime = realtime;
        client.Postgrest = postgrest;
        client.Storage = storage;
        using (new AssertionScope())
        {
            client.Auth.Should().BeSameAs(auth);
            client.Functions.Should().BeSameAs(functions);
            client.Realtime.Should().BeSameAs(realtime);
            client.Postgrest.Should().BeSameAs(postgrest);
            client.Storage.Should().BeSameAs(storage);
        }
    }

    [TestMethod]
    public void SupabaseClient_ShouldComposeApiKeyBearerAndClientInfoHeaders()
    {
        var headers = UrlClient().Postgrest.GetHeaders!();
        using (new AssertionScope())
        {
            headers.Should().ContainKey("apiKey").WhoseValue.Should().Be("test-key");
            headers.Should().ContainKey("Authorization").WhoseValue.Should().Be("Bearer test-key",
                "with no session the bearer falls back to the supabase key");
            headers.Should().ContainKey("X-Client-Info");
        }
    }

    [TestMethod]
    public void SupabaseClient_ShouldPreferDeveloperAuthorizationHeader_GivenAuthorizationInOptions()
    {
        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false,
            Headers =
            {
                ["Authorization"] = "Bearer developer-token"
            }
        };
        UrlClient(options).Postgrest.GetHeaders!().Should().ContainKey("Authorization")
            .WhoseValue.Should().Be("Bearer developer-token",
                "an explicit Authorization header must win over the key-derived bearer (issue #5)");
    }

    [TestMethod]
    public void SupabaseClient_ShouldPreferDeveloperApiKeyHeader_GivenApiKeyInOptions()
    {
        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false,
            Headers =
            {
                ["apikey"] = "developer-key"
            }
        };
        UrlClient(options).Postgrest.GetHeaders!()
            .Where(header => string.Equals(header.Key, "apikey", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle(
                "a developer apikey must replace the key-derived one, not be sent alongside it as a duplicate header that breaks header-based RLS (issue #19)")
            .Which.Value.Should().Be("developer-key");
    }

    [TestMethod]
    [DataRow(AuthState.SignedIn)]
    [DataRow(AuthState.TokenRefreshed)]
    [DataRow(AuthState.UserUpdated)]
    public void SupabaseClient_ShouldForwardAccessTokenToRealtime_GivenAuthStateCarriesSession(AuthState state)
    {
        var realtime = RealtimeSubstitute();
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        auth.CurrentSession.Returns(new Session { AccessToken = "signed-in-token" });
        var client = DiClient(realtime: realtime);
        IGotrueClient<User, Session>.AuthEventHandler? captured = null;
        auth.When(a => a.AddStateChangedListener(Arg.Any<IGotrueClient<User, Session>.AuthEventHandler>()))
            .Do(call => captured = call.Arg<IGotrueClient<User, Session>.AuthEventHandler>());
        client.Auth = auth;
        captured!.Invoke(auth, state);
        realtime.Received().SetAuth("signed-in-token");
    }

    [TestMethod]
    [DataRow(AuthState.SignedOut)]
    [DataRow(AuthState.PasswordRecovery)]
    [DataRow(AuthState.Shutdown)]
    public void SupabaseClient_ShouldNotForwardTokenToRealtime_GivenAuthStateCarriesNoSession(AuthState state)
    {
        var realtime = RealtimeSubstitute();
        realtime.Subscriptions.Returns(new ReadOnlyDictionary<string, RealtimeChannel>(
            new Dictionary<string, RealtimeChannel>()));
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        var client = DiClient(realtime: realtime);
        IGotrueClient<User, Session>.AuthEventHandler? captured = null;
        auth.When(a => a.AddStateChangedListener(Arg.Any<IGotrueClient<User, Session>.AuthEventHandler>()))
            .Do(call => captured = call.Arg<IGotrueClient<User, Session>.AuthEventHandler>());
        client.Auth = auth;
        captured!.Invoke(auth, state);
        realtime.DidNotReceive().SetAuth(Arg.Any<string>());
    }

    [TestMethod]
    public async Task SupabaseClient_ShouldRetrieveSessionAndConnectRealtime_GivenAutoConnectEnabled()
    {
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        var realtime = RealtimeSubstitute();
        var client = DiClient(auth: auth, realtime: realtime,
            options: new SupabaseOptions { AutoConnectRealtime = true });
        await client.InitializeAsync();
        await auth.Received().RetrieveSessionAsync();
        await realtime.Received().ConnectAsync();
    }

    [TestMethod]
    public async Task SupabaseClient_ShouldRestoreThePersistedSessionBeforeRefreshingIt_GivenInitialize()
    {
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        await DiClient(auth: auth).InitializeAsync();
        Received.InOrder(() =>
        {
            auth.LoadSessionAsync();
            auth.RetrieveSessionAsync();
        });
    }

    [TestMethod]
    public async Task SupabaseClient_ShouldNotReloadTheSession_GivenOneAlreadySet()
    {
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        auth.CurrentSession.Returns(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token" });
        await DiClient(auth: auth).InitializeAsync();
        await auth.DidNotReceive().LoadSessionAsync();
    }

    [TestMethod]
    public async Task SupabaseClient_ShouldKeepThePersistedSession_GivenAnOfflineStart()
    {
        var persistence = Substitute.For<IGotrueSessionPersistence<Session>>();
        persistence.LoadSessionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Session?>(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 }));
        var client = new Supabase.Client("http://localhost", "a-key",
            new SupabaseOptions
            {
                AutoConnectRealtime = false,
                SessionHandler = persistence,
                HttpClient = new HttpClient(new UnreachableHandler()),
            });
        await client.InitializeAsync();
        client.Auth.CurrentSession.Should().NotBeNull("an unreachable auth server must not sign the user out");
        await persistence.DidNotReceive().DestroySessionAsync(Arg.Any<CancellationToken>());
        client.Auth.Shutdown();
    }

    [TestMethod]
    public async Task SupabaseClient_ShouldNotConnectRealtime_GivenAutoConnectDisabled()
    {
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        var realtime = RealtimeSubstitute();
        var client = DiClient(auth: auth, realtime: realtime,
            options: new SupabaseOptions { AutoConnectRealtime = false });
        await client.InitializeAsync();
        await auth.Received().RetrieveSessionAsync();
        await realtime.DidNotReceive().ConnectAsync();
    }

    [TestMethod]
    public void SupabaseClient_ShouldDelegateRpcToPostgrest()
    {
        var postgrest = Substitute.For<IPostgrestClient>();
        _ = DiClient(postgrest: postgrest).Rpc("my_function", null);
        postgrest.Received().Rpc("my_function", null);
    }

    [TestMethod]
    public void SupabaseClient_ShouldConfigurePostgrestWithSchema_GivenCustomSchema()
    {
        var client = UrlClient(new SupabaseOptions { AutoConnectRealtime = false, Schema = "custom" });
        client.Postgrest.Options.Schema.Should().Be("custom",
            "the umbrella must pass the developer's schema down to the Postgrest client it builds");
    }

    [TestMethod]
    public void SupabaseClient_ShouldConfigureAuthFromUrlAndOptions_GivenAutoRefreshDisabled()
    {
        var client = UrlClient(new SupabaseOptions { AutoConnectRealtime = false, AutoRefreshToken = false });
        using (new AssertionScope())
        {
            client.Auth.Options.Url.Should().Be("http://localhost/auth/v1",
                "the auth client must be built against the derived auth url, not the Gotrue default");
            client.Auth.Options.AutoRefreshToken.Should().BeFalse(
                "the developer's AutoRefreshToken choice must reach the auth client");
        }
    }

    [TestMethod]
    public void SupabaseClient_ShouldWireTableToClientAuthHeaders_GivenFrom()
    {
        var table = UrlClient().From<Models.Channel>();
        table.GetHeaders!().Should().ContainKey("apiKey").WhoseValue.Should().Be("test-key",
            "queries built from the umbrella must carry its auth headers, or every request is unauthenticated");
    }

    [TestMethod]
    public void SupabaseClient_ShouldDeriveFunctionsUrl_GivenHostedSupabaseUrl()
    {
        var client = new Supabase.Client("https://abcdefgh.supabase.co", "test-key",
            new SupabaseOptions { AutoConnectRealtime = false });
        client.Functions.Should().NotBeNull();
    }

    [TestMethod]
    public void SupabaseClient_ShouldExposeAdminAuthClient_GivenServiceKey() => UrlClient().AdminAuth("service-key").Should().NotBeNull();

    [TestMethod]
    public void SupabaseClient_ShouldPreferSessionTokenOverApiKey_GivenActiveSession()
    {
        var client = UrlClient();
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        auth.CurrentSession.Returns(new Session { AccessToken = "session-token" });
        client.Auth = auth;
        client.Postgrest.GetHeaders!().Should().ContainKey("Authorization")
            .WhoseValue.Should().Be("Bearer session-token",
                "an active session's access token must take precedence over the api key as the bearer");
    }

    private static Supabase.Client NewKeyClient(string key) =>
        new("http://localhost", key, new SupabaseOptions { AutoConnectRealtime = false });

    [TestMethod]
    [DataRow("sb_publishable_abc123")]
    [DataRow("sb_secret_abc123")]
    public void SupabaseClient_ShouldOmitBearerOnFunctionsPath_GivenNewFormatKeyAndNoSession(string key)
    {
        var headers = NewKeyClient(key).Functions.GetHeaders!();
        using (new AssertionScope())
        {
            headers.Should().ContainKey("apiKey").WhoseValue.Should().Be(key);
            headers.Should().NotContainKey("Authorization",
                "an opaque (non-JWT) key must not be sent as a Bearer token to the Edge Functions gateway");
        }
    }

    [TestMethod]
    public void SupabaseClient_ShouldStillSendKeyAsBearerOnDatabasePath_GivenNewFormatKeyAndNoSession()
    {
        // Database/Storage/Realtime/auth keep the key on both headers — the gateway accepts it because
        // the Bearer value exactly equals the apikey value. Only the Functions path is special-cased.
        NewKeyClient("sb_publishable_abc123").Postgrest.GetHeaders!().Should().ContainKey("Authorization")
            .WhoseValue.Should().Be("Bearer sb_publishable_abc123");
    }

    [TestMethod]
    public void SupabaseClient_ShouldUseSessionTokenOnFunctionsPath_GivenNewFormatKeyAndActiveSession()
    {
        var client = NewKeyClient("sb_publishable_abc123");
        var auth = Substitute.For<IGotrueClient<User, Session>>();
        auth.CurrentSession.Returns(new Session { AccessToken = "session-token" });
        client.Auth = auth;
        client.Functions.GetHeaders!().Should().ContainKey("Authorization")
            .WhoseValue.Should().Be("Bearer session-token",
                "with a session the user's access token is a valid JWT bearer even on the Functions path");
    }

    [TestMethod]
    public void SupabaseClient_ShouldSendKeyAsBearerOnFunctionsPath_GivenLegacyKeyAndNoSession()
    {
        NewKeyClient("legacy-jwt-key").Functions.GetHeaders!().Should().ContainKey("Authorization")
            .WhoseValue.Should().Be("Bearer legacy-jwt-key",
                "legacy JWT keys are unchanged — they are still sent as the Bearer stand-in");
    }

    [TestMethod]
    public void SupabaseClient_ShouldPreferDeveloperAuthorizationOnFunctionsPath_GivenNewFormatKey()
    {
        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false,
            Headers = { ["Authorization"] = "Bearer developer-token" }
        };
        new Supabase.Client("http://localhost", "sb_publishable_abc123", options).Functions.GetHeaders!()
            .Should().ContainKey("Authorization").WhoseValue.Should().Be("Bearer developer-token",
                "an explicit Authorization header must win even when the key is a new-format key");
    }

    [TestMethod]
    public void SupabaseClient_ShouldWarnOnUnrecognizedApiKeyFormat()
    {
        var listener = new CapturingTraceListener();
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            // Unique subtype so the once-per-subtype guard doesn't swallow the warning if another
            // test already exercised a shared prefix.
            _ = NewKeyClient("sb_zzztest_abc123");
            listener.Messages.Should().Contain(m => m.Contains("sb_zzztest_"),
                "an unrecognized sb_ key subtype must produce a one-time warning");
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }

    private sealed class CapturingTraceListener : System.Diagnostics.TraceListener
    {
        public List<string> Messages { get; } = new();
        public override void Write(string? message) => this.Messages.Add(message ?? string.Empty);
        public override void WriteLine(string? message) => this.Messages.Add(message ?? string.Empty);
    }

    [TestMethod]
    public void SupabaseClient_ShouldStopListeningToPreviousAuth_GivenAuthReplaced()
    {
        var previous = Substitute.For<IGotrueClient<User, Session>>();
        var client = DiClient(auth: previous);
        client.Auth = Substitute.For<IGotrueClient<User, Session>>();
        previous.Received().RemoveStateChangedListener(Arg.Any<IGotrueClient<User, Session>.AuthEventHandler>());
    }

    [TestMethod]
    public void SupabaseClient_ShouldDisconnectPreviousRealtime_GivenRealtimeReplaced()
    {
        var previous = RealtimeSubstitute();
        var client = DiClient(realtime: previous);
        client.Realtime = Substitute.For<IRealtimeClient<RealtimeSocket, RealtimeChannel>>();
        previous.Received().Disconnect(Arg.Any<WebSocketCloseStatus>(), Arg.Any<string>());
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
