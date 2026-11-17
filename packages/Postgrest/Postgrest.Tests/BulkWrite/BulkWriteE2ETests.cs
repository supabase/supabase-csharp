using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Support;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.BulkWrite;

/// <summary>
///     Bulk inserts and upserts whose rows carry different fields, against a live Postgrest.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class BulkWriteE2ETests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Insert_ShouldWriteEveryRow_GivenRowsWithDifferingFields()
    {
        var client = LocalStack.Client();
        var first = new SparseUser { Username = Guid.NewGuid().ToString(), Data = "{\"a\":1}" };
        var second = new SparseUser { Username = Guid.NewGuid().ToString(), Catchphrase = "hello" };
        try
        {
            var response = await client.Table<SparseUser>().Insert(new List<SparseUser> { first, second }, cancellationToken: this.TestContext.CancellationToken);
            response.Models.Select(row => row.Username).Should().BeEquivalentTo(new[] { first.Username, second.Username });
        }
        finally
        {
            await this.Remove(client, first, second);
        }
    }

    [TestMethod]
    public async Task Upsert_ShouldWriteEveryRow_GivenRowsWithDifferingFields()
    {
        var client = LocalStack.Client();
        var first = new SparseUser { Username = Guid.NewGuid().ToString(), Data = "{\"a\":1}" };
        var second = new SparseUser { Username = Guid.NewGuid().ToString(), Catchphrase = "hello" };
        try
        {
            var response = await client.Table<SparseUser>().Upsert(new List<SparseUser> { first, second }, cancellationToken: this.TestContext.CancellationToken);
            response.Models.Select(row => row.Username).Should().BeEquivalentTo(new[] { first.Username, second.Username });
        }
        finally
        {
            await this.Remove(client, first, second);
        }
    }

    private async Task Remove(Client client, params SparseUser[] rows)
    {
        foreach (var row in rows)
            await client.Table<SparseUser>().Filter("username", Operator.Equals, row.Username!).Delete(cancellationToken: this.TestContext.CancellationToken);
    }
}
