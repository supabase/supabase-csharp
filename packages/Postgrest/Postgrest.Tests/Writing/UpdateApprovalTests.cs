using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.Writing;

/// <summary>
///     Pins the exact bytes an update (PATCH) puts on the wire. The resolver runs in its update state here,
///     which drops columns differently from insert, and a <c>Set</c>-clause update sends only the assigned
///     columns rather than the whole model — both are serialization behaviours the System.Text.Json migration
///     must preserve.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task UpdateRequest_ShouldSerializeTheModel_GivenAFullModel()
    {
        await this.Client.Table<Todo>().Filter("id", Operator.Equals, "1")
            .Update(new Todo { Name = "walk the dog", Status = Todo.TodoStatus.DONE });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateRequest_ShouldSerializeOnlyTheAssignedColumns_GivenSetClauses()
    {
        await this.Client.Table<Todo>().Filter("id", Operator.Equals, "1")
            .Set(todo => todo.Name!, "renamed")
            .Set(todo => todo.Done, true)
            .Update();
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateRequest_ShouldIncludeExplicitNull_GivenAJsonColumnSetToNull()
    {
        await this.Client.Table<UserWithJsonData>().Filter("username", Operator.Equals, "null-set-target")
            .Set(user => user.Data!, null)
            .Update();
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
