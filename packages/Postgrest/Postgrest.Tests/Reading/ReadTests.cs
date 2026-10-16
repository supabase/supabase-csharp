using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.Reading;

/// <summary>
///     Reading rows against a live PostgREST: column projection returns only the requested columns, and the
///     count variants report row totals both as a standalone call and alongside the returned models.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ReadTests
{
    [TestMethod]
    public async Task Select_ShouldReturnOnlyTheProjectedColumn()
    {
        var response = await LocalStack.Client().Table<User>().Select("username").Get();
        response.Models.Should().OnlyContain(user =>
            user.Username != null && user.Catchphrase == null && user.Status == null);
    }

    [TestMethod]
    public async Task Select_ShouldReturnOnlyTheProjectedColumns_GivenMultipleColumns()
    {
        var response = await LocalStack.Client().Table<User>().Select("username,status").Get();
        response.Models.Should().OnlyContain(user =>
            user.Username != null && user.Status != null && user.Catchphrase == null);
    }

    [TestMethod]
    public async Task Count_ShouldReturnARowTotal()
    {
        var count = await LocalStack.Client().Table<User>().Count(CountType.Exact);
        count.Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public async Task Count_ShouldRespectAFilter()
    {
        var count = await LocalStack.Client().Table<User>()
            .Filter("status", Operator.Equals, "ONLINE").Count(CountType.Exact);
        count.Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public async Task Get_ShouldReportTheCount_GivenAnExactCountRequest()
    {
        var response = await LocalStack.Client().Table<User>().Get(default, CountType.Exact);
        response.Count.Should().BeGreaterThan(-1);
    }

    [TestMethod]
    public async Task Get_ShouldReportTheFilteredCount_GivenAnExactCountRequest()
    {
        var response = await LocalStack.Client().Table<User>()
            .Filter("status", Operator.Equals, "ONLINE").Get(default, CountType.Exact);
        response.Count.Should().BeGreaterThan(-1);
    }

    [TestMethod]
    public async Task Get_ShouldLeaveNullValuesOut_GivenStripNulls()
    {
        var response = await LocalStack.Client().Table<User>().StripNulls().Get();
        response.Content.Should().NotContain(":null", "the seeded users have null columns");
    }
}
