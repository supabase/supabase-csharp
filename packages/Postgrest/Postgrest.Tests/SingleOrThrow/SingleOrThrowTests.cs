using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest.Exceptions;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.SingleOrThrow;

/// <summary>
///     Against the live stack, <c>SingleOrThrowAsync</c> returns the one row a query matches, and a query that
///     matches no row or several rows is refused by PostgREST with <c>PGRST116</c>.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SingleOrThrowTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldReturnTheRow_GivenExactlyOneRowMatches()
    {
        var client = LocalStack.Client();
        var prefix = $"single-{Guid.NewGuid():N}";
        await client.Table<User>().Insert(Users(prefix, 1), cancellationToken: this.TestContext.CancellationToken);
        try
        {
            (await client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").SingleOrThrowAsync(this.TestContext.CancellationToken))
                .Username.Should().Be($"{prefix}-0");
        }
        finally
        {
            await Delete(client, prefix);
        }
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldThrowUnexpectedRowCount_GivenNoRowMatches()
    {
        var act = () => LocalStack.Client().Table<User>().Filter("username", Operator.Equals, $"none-{Guid.NewGuid():N}").SingleOrThrowAsync(this.TestContext.CancellationToken);
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason.Should().Be(FailureHint.Reason.UnexpectedRowCount);
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldThrowUnexpectedRowCount_GivenSeveralRowsMatch()
    {
        var client = LocalStack.Client();
        var prefix = $"single-{Guid.NewGuid():N}";
        await client.Table<User>().Insert(Users(prefix, 2), cancellationToken: this.TestContext.CancellationToken);
        try
        {
            var act = () => client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").SingleOrThrowAsync(this.TestContext.CancellationToken);
            (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason.Should().Be(FailureHint.Reason.UnexpectedRowCount);
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
