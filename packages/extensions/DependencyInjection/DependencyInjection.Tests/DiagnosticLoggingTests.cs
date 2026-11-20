using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase;
using Supabase.Extensions.DependencyInjection;

namespace DependencyInjection.Tests;

/// <summary>
/// <see cref="SupabaseServiceCollectionExtensions.AddSupabase"/> gives the client the container's <see cref="ILoggerFactory"/> when
/// one is registered and the caller has not set their own (client.observability.diagnostic_logging).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DiagnosticLoggingTests
{
    private const string SupabaseUrl = "http://localhost:54321";
    private const string SupabaseKey = "test-key";

    private sealed class MarkedLoggerFactory : ILoggerFactory
    {
        internal static readonly ILogger Logger = new NullLogger<MarkedLoggerFactory>();

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => Logger;

        public void Dispose()
        {
        }
    }

    private static Client Resolve(ServiceCollection services, System.Action<SupabaseOptions>? configure = null)
    {
        services.AddSupabase(SupabaseUrl, SupabaseKey, configure);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<Client>();
    }

    [TestMethod]
    public void AddSupabase_ShouldUseTheContainersLoggerFactory_GivenOneIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new MarkedLoggerFactory());
        Resolve(services).Postgrest.Options.Logger.Should().BeSameAs(MarkedLoggerFactory.Logger, "the container's logging reaches the SDK");
    }

    [TestMethod]
    public void AddSupabase_ShouldKeepTheCallersLoggerFactory_GivenBothAreSet()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new MarkedLoggerFactory());
        Resolve(services, options => options.LoggerFactory = NullLoggerFactory.Instance).Postgrest.Options.Logger
            .Should().BeSameAs(NullLogger.Instance, "a factory set through configureOptions wins over the container's");
    }
}
