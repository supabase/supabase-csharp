using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Supabase.Tests.Observability;

/// <summary>
/// <see cref="SupabaseDiagnostics.SourceNames"/> lists the source and meter name of every instrumented client, so one
/// registration covers them all.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SupabaseDiagnosticsTests
{
    [TestMethod]
    public void SourceNames_ShouldListEveryInstrumentedClient() =>
        SupabaseDiagnostics.SourceNames.Should().BeEquivalentTo(
            new[] { "supabase.gotrue", "supabase.postgrest", "supabase.functions", "supabase.storage" },
            "Auth, Database, Functions and Storage each emit under their own lowercase name, and Realtime emits no spans or metrics");
}
