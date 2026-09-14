using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase;
using Supabase.Core.Http;
using Supabase.Extensions.DependencyInjection;

namespace DependencyInjection.Tests;

/// <summary>
/// <see cref="SupabaseServiceCollectionExtensions.AddSupabase"/> adds no new injection seams of its own —
/// every knob it touches (<c>ClientOptions.HttpClient</c> per package, Storage's three named clients,
/// <see cref="SupabaseOptions"/>' passthrough) already exists and is covered elsewhere. These tests instead
/// pin the two things that are actually new: that the wiring reaches every sub-client correctly, and — the
/// entire point of this package — that <see cref="IHttpClientFactory"/> genuinely pools the underlying
/// transport across resolutions instead of the SDK building a fresh one per <c>Client</c>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SupabaseServiceCollectionExtensionsTests
{
    private const string SupabaseUrl = "http://localhost:54321";
    private const string SupabaseKey = "test-key";

    [TestMethod]
    public void AddSupabase_ShouldResolveAClientAndEverySubClient()
    {
        var services = new ServiceCollection();
        services.AddSupabase(SupabaseUrl, SupabaseKey);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<Client>().Should().NotBeNull();
        sp.GetRequiredService<Client>().Auth.Should().NotBeNull();
        sp.GetRequiredService<Client>().Postgrest.Should().NotBeNull();
        sp.GetRequiredService<Client>().Storage.Should().NotBeNull();
        sp.GetRequiredService<Client>().Functions.Should().NotBeNull();
        sp.GetRequiredService<Client>().Realtime.Should().NotBeNull();
    }

    [TestMethod]
    public void AddSupabase_ShouldWireFactoryCreatedHttpClients_IntoAuthPostgrestFunctionsAndStorage()
    {
        var services = new ServiceCollection();
        services.AddSupabase(SupabaseUrl, SupabaseKey);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<Client>();

        client.Auth.Options.HttpClient.Should().NotBeNull("Auth must send through the factory-pooled client, not build its own");
        client.Postgrest.Options.HttpClient.Should().NotBeNull("Postgrest must send through the factory-pooled client, not build its own");
        ((Supabase.Functions.Client) client.Functions).Options.HttpClient.Should().NotBeNull("Functions must send through the factory-pooled client, not build its own");
        client.Auth.Options.HttpClient.Should().BeSameAs(client.Postgrest.Options.HttpClient,
            "Auth, Postgrest and Functions share one named client, not one each");

        client.Storage.Options.HttpRequestClient.Should().NotBeNull();
        client.Storage.Options.HttpUploadClient.Should().NotBeNull();
        client.Storage.Options.HttpDownloadClient.Should().NotBeNull();
        client.Storage.Options.HttpRequestClient.Should().NotBeSameAs(client.Storage.Options.HttpUploadClient,
            "Storage's request/upload/download clients are independently named — a shared instance would collapse their independent timeout profiles");
    }

    [TestMethod]
    public void AddSupabase_ShouldApplyConfigureOptions_ButOverwriteAnyHttpClientItSets()
    {
        var callerSuppliedClient = new HttpClient();
        var retry = new RetryOptions { MaxRetries = 4 };
        var services = new ServiceCollection();
        services.AddSupabase(SupabaseUrl, SupabaseKey, options =>
        {
            options.PostgrestRetry = retry;
            options.HttpClient = callerSuppliedClient;
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<Client>();

        client.Postgrest.Options.Retry.Should().BeSameAs(retry, "plain configuration must reach the sub-clients unchanged");
        client.Postgrest.Options.HttpClient.Should().NotBeSameAs(callerSuppliedClient,
            "AddSupabase must supply the factory-pooled client even if configureOptions set one directly — that overwrite is documented on the method");
    }

    [TestMethod]
    public void AddSupabase_ShouldReuseThePooledHandler_AcrossMultipleScopes()
    {
        CountingHandler.ConstructedCount = 0;
        var services = new ServiceCollection();
        services.AddSupabase(SupabaseUrl, SupabaseKey);
        services.AddHttpClient("Supabase").AddHttpMessageHandler(() => new CountingHandler());

        using var provider = services.BuildServiceProvider();

        using (var scope1 = provider.CreateScope())
            _ = scope1.ServiceProvider.GetRequiredService<Client>();

        using (var scope2 = provider.CreateScope())
            _ = scope2.ServiceProvider.GetRequiredService<Client>();

        CountingHandler.ConstructedCount.Should().Be(1,
            "the handler pipeline must be pooled across scope disposal, not rebuilt per Client resolution — " +
            "that pooling is the actual socket-exhaustion fix this package exists for");
    }

    private sealed class CountingHandler : DelegatingHandler
    {
        public static int ConstructedCount;

        public CountingHandler() => Interlocked.Increment(ref ConstructedCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
