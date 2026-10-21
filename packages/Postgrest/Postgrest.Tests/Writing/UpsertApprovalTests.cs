using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest;

namespace Postgrest.Tests.Writing;

/// <summary>
///     Pins the exact bytes an upsert puts on the wire. The resolver runs in its upsert state, which — unlike
///     insert — serializes the primary key so the server can match an existing row. Pinning this guards the
///     upsert payload through the System.Text.Json migration.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpsertApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task UpsertRequest_ShouldIncludeThePrimaryKey_GivenTodo()
    {
        await this.Client.Table<Todo>().Upsert(new Todo { Id = 7, Name = "walk the dog", Status = Todo.TodoStatus.DONE });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpsertRequest_ShouldSerializeToExpectedPayload_GivenOnConflictColumn()
    {
        await this.Client.Table<KitchenSink>().OnConflict("unique_value")
            .Upsert(new KitchenSink { UniqueValue = "unique", StringValue = "text" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task Upsert_ShouldLeaveOptionsUnchanged_GivenReusedOptions()
    {
        var options = new QueryOptions { DuplicateResolution = QueryOptions.DuplicateResolutionType.IgnoreDuplicates };
        var todo = new Todo { Id = 7, Name = "walk the dog" };
        await this.Client.Table<Todo>().Upsert(todo, options);
        await this.Client.Table<Todo>().Upsert(new List<Todo> { todo }, options);
        await this.Client.Table<Todo>().Insert(todo, options);
        this.EmittedPreferHeaders.Should().Equal(
            "resolution=ignore-duplicates,return=representation",
            "resolution=ignore-duplicates,return=representation",
            "return=representation");
    }
}
