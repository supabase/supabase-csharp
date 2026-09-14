using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Supabase.Tests;

/// <summary>
/// The defaults on <see cref="SupabaseOptions"/> are a public contract: they are what a client built with
/// no configuration points at (the hosted Supabase URL conventions) and how it behaves out of the box.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SupabaseOptionsTests
{
    [TestMethod]
    public void SupabaseOptions_ShouldDefaultToHostedPlatformConventions()
    {
        var options = new SupabaseOptions();
        using (new AssertionScope())
        {
            options.Schema.Should().Be("public");
            options.AutoRefreshToken.Should().BeTrue();
            options.AuthUrlFormat.Should().Be("{0}/auth/v1");
            options.RestUrlFormat.Should().Be("{0}/rest/v1");
            options.RealtimeUrlFormat.Should().Be("{0}/realtime/v1");
            options.StorageUrlFormat.Should().Be("{0}/storage/v1");
            options.FunctionsUrlFormat.Should().Be("{0}/functions/v1");
            options.PostgrestRetry.MaxRetries.Should().Be(0);
            options.GotrueRetry.MaxRetries.Should().Be(0);
            options.FunctionsRetry.MaxRetries.Should().Be(0);
            options.HttpClient.Should().BeNull();
            options.Proxy.Should().BeNull();
            options.WebSocketFactory.Should().BeNull();
        }
    }
}
