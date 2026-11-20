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

namespace Postgrest.Tests.LimitAffectedRows;

/// <summary>
///     Against the live stack, a mutation that would affect more rows than <c>MaxAffected</c> allows is rolled
///     back, and one within the limit goes through.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class MaxAffectedTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Delete_ShouldRollBackAndThrow_GivenMoreRowsThanTheLimit()
    {
        var client = LocalStack.Client();
        var prefix = $"maxaff-{Guid.NewGuid():N}";
        await client.Table<User>().Insert(Users(prefix, 2), cancellationToken: this.TestContext.CancellationToken);
        try
        {
            var act = () => client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").MaxAffected(1).Delete(cancellationToken: this.TestContext.CancellationToken);
            (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason.Should().Be(FailureHint.Reason.MaxAffectedExceeded);
            (await client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").Get(this.TestContext.CancellationToken))
                .Models.Should().HaveCount(2, "the refused delete is rolled back");
        }
        finally
        {
            await client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").Delete(cancellationToken: this.TestContext.CancellationToken);
        }
    }

    [TestMethod]
    public async Task Update_ShouldRollBackAndThrow_GivenMoreRowsThanTheLimit()
    {
        var client = LocalStack.Client();
        var prefix = $"maxaff-{Guid.NewGuid():N}";
        await client.Table<User>().Insert(Users(prefix, 2), cancellationToken: this.TestContext.CancellationToken);
        try
        {
            var act = () => client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").Set(x => x.Status!, "OFFLINE").MaxAffected(1).Update(cancellationToken: this.TestContext.CancellationToken);
            (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason.Should().Be(FailureHint.Reason.MaxAffectedExceeded);
            (await client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").Get(this.TestContext.CancellationToken))
                .Models.Should().OnlyContain(user => user.Status == "ONLINE");
        }
        finally
        {
            await client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").Delete(cancellationToken: this.TestContext.CancellationToken);
        }
    }

    [TestMethod]
    public async Task Delete_ShouldDeleteTheRows_GivenTheLimitCoversThem()
    {
        var client = LocalStack.Client();
        var prefix = $"maxaff-{Guid.NewGuid():N}";
        await client.Table<User>().Insert(Users(prefix, 2), cancellationToken: this.TestContext.CancellationToken);
        var response = await client.Table<User>().Filter("username", Operator.Like, $"{prefix}%").MaxAffected(2).Delete(cancellationToken: this.TestContext.CancellationToken);
        response.Models.Should().HaveCount(2);
    }

    private static List<User> Users(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => new User { Username = $"{prefix}-{i}", Status = "ONLINE" }).ToList();
}
