using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.GetCsv;

/// <summary>
///     Against the live stack, <c>GetCsvAsync</c> returns the selected columns of the matching rows as CSV, and
///     composes with ordering and ranges.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class GetCsvTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetCsvAsync_ShouldReturnTheMatchingRowsAsCsv()
    {
        var client = LocalStack.Client();
        var prefix = $"csv-{Guid.NewGuid():N}";
        await client.Table<User>().Insert(Users(prefix, 2), cancellationToken: this.TestContext.CancellationToken);
        try
        {
            (await client.Table<User>().Select("username,status").Filter("username", Operator.Like, $"{prefix}%")
                    .Order("username", Ordering.Ascending).GetCsvAsync(this.TestContext.CancellationToken))
                .Should().Be($"username,status\n{prefix}-0,ONLINE\n{prefix}-1,ONLINE");
        }
        finally
        {
            await Delete(client, prefix);
        }
    }

    [TestMethod]
    public async Task GetCsvAsync_ShouldReturnOnlyTheRequestedRange_GivenRange()
    {
        var client = LocalStack.Client();
        var prefix = $"csv-{Guid.NewGuid():N}";
        await client.Table<User>().Insert(Users(prefix, 3), cancellationToken: this.TestContext.CancellationToken);
        try
        {
            (await client.Table<User>().Select("username").Filter("username", Operator.Like, $"{prefix}%")
                    .Order("username", Ordering.Ascending).Range(1, 1).GetCsvAsync(this.TestContext.CancellationToken))
                .Should().Be($"username\n{prefix}-1");
        }
        finally
        {
            await Delete(client, prefix);
        }
    }

    private static Task Delete(Supabase.Postgrest.Client client, string prefix) =>
        client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").Delete();

    private static List<User> Users(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => new User { Username = $"{prefix}-{i}", Status = "ONLINE" }).ToList();
}
