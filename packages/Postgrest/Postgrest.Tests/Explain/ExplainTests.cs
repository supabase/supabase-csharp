using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest.Explain;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.Explain;

/// <summary>
///     Against the live stack, with <c>pgrst.db_plan_enabled</c> on, <c>ExplainAsync</c> returns the plan of the
///     read instead of its rows, and an analyzed plan carries the measured execution time.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ExplainTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExplainAsync_ShouldReturnThePlanOfTheRead()
    {
        var plan = await LocalStack.Client().Table<User>().Filter("username", Operator.Equals, "supabot")
            .ExplainAsync(cancellationToken: this.TestContext.CancellationToken);
        plan[0].GetProperty("Plan").GetProperty("Node Type").GetString()
            .Should().Be("Aggregate", "PostgREST wraps the planned read in an aggregate that builds the JSON body");
    }

    [TestMethod]
    public async Task ExplainAsync_ShouldReportTheExecutionTime_GivenAnalyze()
    {
        var plan = await LocalStack.Client().Table<User>()
            .ExplainAsync(new ExplainOptions { Analyze = true }, this.TestContext.CancellationToken);
        plan[0].TryGetProperty("Execution Time", out _)
            .Should().BeTrue("EXPLAIN ANALYZE runs the query and reports how long it took");
    }
}
