using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Supabase.Tests.Observability;

/// <summary>
/// <see cref="SupabaseOptions.LoggerFactory"/> gives every sub-client the logger of its own category, in the umbrella client and
/// the stateless facade, and a logger set on the Storage options is kept (client.observability.diagnostic_logging).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DiagnosticLoggingWiringTests
{
    private static ILoggerFactory FactoryOfDistinctLoggers()
    {
        var factory = Substitute.For<ILoggerFactory>();
        factory.CreateLogger(Arg.Any<string>()).Returns(_ => Substitute.For<ILogger>());
        return factory;
    }

    [TestMethod]
    public void Client_ShouldHandEachSubClientALoggerOfItsOwnCategory_GivenALoggerFactory()
    {
        var factory = FactoryOfDistinctLoggers();
        var client = new Supabase.Client("http://localhost", "test-key", new SupabaseOptions { LoggerFactory = factory });
        using (new AssertionScope())
        {
            client.Auth.Options.Logger.Should().NotBeNull();
            client.Postgrest.Options.Logger.Should().NotBeNull();
            ((Supabase.Functions.Client) client.Functions).Options.Logger.Should().NotBeNull();
            client.Realtime.Options.Logger.Should().NotBeNull();
            client.Storage.Options.Logger.Should().NotBeNull();
            factory.Received(1).CreateLogger("supabase.gotrue");
            factory.Received(1).CreateLogger("supabase.postgrest");
            factory.Received(1).CreateLogger("supabase.functions");
            factory.Received(1).CreateLogger("supabase.realtime");
            factory.Received(1).CreateLogger("supabase.storage");
        }
    }

    [TestMethod]
    public void Client_ShouldLogNothing_GivenNoLoggerFactory()
    {
        var client = new Supabase.Client("http://localhost", "test-key", new SupabaseOptions());
        using (new AssertionScope())
        {
            client.Auth.Options.Logger.Should().BeNull("the SDK is silent unless a logger is supplied");
            client.Postgrest.Options.Logger.Should().BeNull();
            ((Supabase.Functions.Client) client.Functions).Options.Logger.Should().BeNull();
            client.Realtime.Options.Logger.Should().BeNull();
            client.Storage.Options.Logger.Should().BeNull();
        }
    }

    [TestMethod]
    public void Client_ShouldKeepTheStorageLogger_GivenOneInTheStorageOptions()
    {
        var storageLogger = Substitute.For<ILogger>();
        var client = new Supabase.Client("http://localhost", "test-key", new SupabaseOptions
        {
            LoggerFactory = FactoryOfDistinctLoggers(),
            StorageClientOptions = new Storage.ClientOptions { Logger = storageLogger }
        });
        client.Storage.Options.Logger.Should().BeSameAs(storageLogger, "a logger set on the Storage options is not replaced");
    }

    [TestMethod]
    public void Client_ShouldHandTheAdminClientAnAuthLogger_GivenALoggerFactory()
    {
        var factory = FactoryOfDistinctLoggers();
        new Supabase.Client("http://localhost", "test-key", new SupabaseOptions { LoggerFactory = factory }).AdminAuth("service-key");
        factory.Received(2).CreateLogger("supabase.gotrue");
    }

    [TestMethod]
    public void StatelessClient_ShouldHandEachSubClientALogger_GivenALoggerFactory()
    {
        var options = new SupabaseOptions { LoggerFactory = FactoryOfDistinctLoggers() };
        using (new AssertionScope())
        {
            StatelessClient.GetAuthOptions("http://localhost", "test-key", options).Logger.Should().NotBeNull();
            StatelessClient.GetRestOptions("test-key", options).Logger.Should().NotBeNull();
            ((Supabase.Functions.Client) StatelessClient.Functions("http://localhost", "test-key", options)).Options.Logger.Should().NotBeNull();
            StatelessClient.Storage("http://localhost", "test-key", options).Options.Logger.Should().NotBeNull();
        }
    }
}
