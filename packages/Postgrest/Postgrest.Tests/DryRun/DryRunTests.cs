using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.DryRun;

/// <summary>
///     Against the live stack, with <c>pgrst.db_tx_end</c> set to <c>commit-allow-override</c>, a dry run
///     returns what the write would have produced and leaves the table as it was.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class DryRunTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Insert_ShouldReturnTheRowItWouldHaveInserted_GivenDryRun()
    {
        var username = $"dry-run-{Guid.NewGuid():N}";
        (await LocalStack.Client().Table<User>().DryRun()
                .Insert(new User { Username = username, Status = "ONLINE" }, cancellationToken: this.TestContext.CancellationToken))
            .Model!.Username.Should().Be(username, "the RETURNING clause runs before the rollback");
    }

    [TestMethod]
    public async Task Insert_ShouldNotKeepTheRow_GivenDryRun()
    {
        var client = LocalStack.Client();
        var username = $"dry-run-{Guid.NewGuid():N}";
        await client.Table<User>().DryRun().Insert(new User { Username = username, Status = "ONLINE" }, cancellationToken: this.TestContext.CancellationToken);
        (await client.Table<User>().Filter("username", Operator.Equals, username).Get(this.TestContext.CancellationToken))
            .Models.Should().BeEmpty("PostgREST rolled the insert back");
    }

    [TestMethod]
    public async Task Update_ShouldLeaveTheRowUnchanged_GivenDryRun()
    {
        var client = LocalStack.Client();
        var username = $"dry-run-{Guid.NewGuid():N}";
        await client.Table<User>().Insert(new User { Username = username, Status = "ONLINE" }, cancellationToken: this.TestContext.CancellationToken);
        try
        {
            await client.Table<User>().Filter("username", Operator.Equals, username).Set(x => x.Status!, "OFFLINE")
                .DryRun().Update(cancellationToken: this.TestContext.CancellationToken);
            (await client.Table<User>().Filter("username", Operator.Equals, username).Single(this.TestContext.CancellationToken))!
                .Status.Should().Be("ONLINE", "PostgREST rolled the update back");
        }
        finally
        {
            await client.Table<User>().Filter("username", Operator.Equals, username).Delete();
        }
    }
}
