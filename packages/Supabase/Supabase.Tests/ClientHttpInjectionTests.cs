using System;
using System.Collections.Generic;
using System.Net.Http;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;
using Supabase.Realtime.Sockets;
using Websocket.Client;

namespace Supabase.Tests;

/// <summary>
/// Pins that <see cref="SupabaseOptions"/>' HttpClient/Proxy/Retry/WebSocketFactory knobs actually reach
/// the sub-clients the convenience <see cref="Client(string, string?, SupabaseOptions?)"/> constructor
/// builds — the passthrough is the entire point of these options; a typo in the wiring would silently
/// leave a sub-client building its own default transport instead of the one the caller configured.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ClientHttpInjectionTests
{
    [TestMethod]
    public void Client_ShouldMirrorHttpClientAndProxyToAuthPostgrestAndFunctions()
    {
        using var httpClient = new HttpClient();
        var options = new SupabaseOptions { HttpClient = httpClient };

        var client = new Client("http://localhost:54321", "test-key", options);

        using (new AssertionScope())
        {
            client.Auth.Options.HttpClient.Should().BeSameAs(httpClient, "Auth must send through the shared injected client");
            client.Postgrest.Options.HttpClient.Should().BeSameAs(httpClient, "Postgrest must send through the shared injected client");
            ((Supabase.Functions.Client) client.Functions).Options.HttpClient.Should().BeSameAs(httpClient,
                "Functions must send through the shared injected client");
        }
    }

    [TestMethod]
    public void Client_ShouldMirrorPerPackageRetryPoliciesToTheirRespectiveClients()
    {
        var gotrueRetry = new RetryOptions { MaxRetries = 3 };
        var postgrestRetry = new RetryOptions { MaxRetries = 5 };
        var functionsRetry = new RetryOptions { MaxRetries = 7 };
        var options = new SupabaseOptions
        {
            GotrueRetry = gotrueRetry,
            PostgrestRetry = postgrestRetry,
            FunctionsRetry = functionsRetry
        };

        var client = new Client("http://localhost:54321", "test-key", options);

        using (new AssertionScope())
        {
            client.Auth.Options.Retry.Should().BeSameAs(gotrueRetry, "Auth must use its own configured retry policy, not another package's");
            client.Postgrest.Options.Retry.Should().BeSameAs(postgrestRetry);
            ((Supabase.Functions.Client) client.Functions).Options.Retry.Should().BeSameAs(functionsRetry);
        }
    }

    [TestMethod]
    public void Client_ShouldMirrorTimeProviderToEveryChildClient()
    {
        var clock = new FakeTimeProvider();
        var client = new Client("http://localhost:54321", "test-key", new SupabaseOptions { TimeProvider = clock });
        using (new AssertionScope())
        {
            client.Auth.Options.TimeProvider.Should().BeSameAs(clock, "the auth refresh timer and retry backoff must wait on the supplied clock");
            client.Postgrest.Options.TimeProvider.Should().BeSameAs(clock, "the request timeout and retry backoff must wait on the supplied clock");
            ((Supabase.Functions.Client) client.Functions).Options.TimeProvider.Should().BeSameAs(clock, "the invocation timeout and retry backoff must wait on the supplied clock");
            ((Supabase.Storage.Client) client.Storage).Options.TimeProvider.Should().BeSameAs(clock, "the retry backoff must wait on the supplied clock");
            client.Realtime.Options.TimeProvider.Should().BeSameAs(clock, "every realtime timer must wait on the supplied clock");
        }
    }

    [TestMethod]
    public void Client_ShouldMirrorTimeProviderToAdminAuth()
    {
        var clock = new FakeTimeProvider();
        var admin = new Client("http://localhost:54321", "test-key", new SupabaseOptions { TimeProvider = clock }).AdminAuth("service-key");
        ((Supabase.Gotrue.AdminClient) admin).Options.TimeProvider.Should().BeSameAs(clock, "admin auth retry backoff must wait on the supplied clock");
    }

    [TestMethod]
    public void Client_ShouldLeaveEveryChildClientOnTheSystemClock_GivenNoTimeProvider()
    {
        var client = new Client("http://localhost:54321", "test-key", new SupabaseOptions());
        using (new AssertionScope())
        {
            client.Auth.Options.TimeProvider.Should().BeSameAs(TimeProvider.System);
            client.Postgrest.Options.TimeProvider.Should().BeSameAs(TimeProvider.System);
            ((Supabase.Functions.Client) client.Functions).Options.TimeProvider.Should().BeSameAs(TimeProvider.System);
            ((Supabase.Storage.Client) client.Storage).Options.TimeProvider.Should().BeSameAs(TimeProvider.System);
            client.Realtime.Options.TimeProvider.Should().BeSameAs(TimeProvider.System);
        }
    }

    [TestMethod]
    public void Client_ShouldKeepTheStorageTimeProvider_GivenNoTimeProviderOnSupabaseOptions()
    {
        var storageClock = new FakeTimeProvider();
        var options = new SupabaseOptions { StorageClientOptions = new Supabase.Storage.ClientOptions { TimeProvider = storageClock } };
        ((Supabase.Storage.Client) new Client("http://localhost:54321", "test-key", options).Storage).Options.TimeProvider.Should().BeSameAs(storageClock);
    }

    [TestMethod]
    public void Client_ShouldPreferTheSupabaseTimeProviderToTheStorageOne_GivenBoth()
    {
        var supabaseClock = new FakeTimeProvider();
        var options = new SupabaseOptions
        {
            TimeProvider = supabaseClock,
            StorageClientOptions = new Supabase.Storage.ClientOptions { TimeProvider = new FakeTimeProvider() }
        };
        ((Supabase.Storage.Client) new Client("http://localhost:54321", "test-key", options).Storage).Options.TimeProvider.Should().BeSameAs(supabaseClock);
    }

    [TestMethod]
    public void Client_ShouldMirrorPostgrestTimeoutToPostgrest() =>
        new Client("http://localhost:54321", "test-key", new SupabaseOptions { PostgrestTimeout = TimeSpan.FromSeconds(5) })
            .Postgrest.Options.Timeout.Should().Be(TimeSpan.FromSeconds(5));

    [TestMethod]
    public void Client_ShouldMirrorWebSocketFactoryToRealtime()
    {
        var factory = new FakeWebSocketFactory();
        var options = new SupabaseOptions { WebSocketFactory = factory };

        var client = new Client("http://localhost:54321", "test-key", options);

        client.Realtime.Options.WebSocketFactory.Should().BeSameAs(factory,
            "Realtime must connect through the caller-supplied transport factory");
    }

    private sealed class FakeWebSocketFactory : IWebSocketFactory
    {
        public IWebsocketClient Create(Uri uri, Func<Dictionary<string, string>> headers) =>
            throw new NotImplementedException("Not exercised — this test only checks that the factory reference is threaded through.");
    }
}
