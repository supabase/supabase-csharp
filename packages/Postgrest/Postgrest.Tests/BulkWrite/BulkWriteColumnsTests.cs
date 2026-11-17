using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Support;

namespace Postgrest.Tests.BulkWrite;

/// <summary>
///     Pins the <c>columns</c> query parameter and the body a bulk insert or upsert puts on the wire when its rows
///     carry different fields.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class BulkWriteColumnsTests : RequestApprovalFixture
{
    private static List<SparseUser> RowsWithDifferingFields() => new()
    {
        new SparseUser { Username = "ann", Data = "{}" },
        new SparseUser { Username = "bob", Catchphrase = "hi" }
    };

    [TestMethod]
    public async Task Insert_ShouldNameTheUnionOfFields_GivenRowsWithDifferingFields()
    {
        await this.Client.Table<SparseUser>().Insert(RowsWithDifferingFields());
        this.EmittedRequestQuery["columns"].Should()
            .Be("\"username\",\"data\",\"catchphrase\"", "PostgREST rejects rows with different keys with PGRST102 unless columns names them all (bulk_rows_with_differing_fields spec)");
    }

    [TestMethod]
    public async Task Upsert_ShouldNameTheUnionOfFields_GivenRowsWithDifferingFields()
    {
        await this.Client.Table<SparseUser>().Upsert(RowsWithDifferingFields());
        this.EmittedRequestQuery["columns"].Should()
            .Be("\"username\",\"data\",\"catchphrase\"", "upsert is a bulk write and follows the same rule (bulk_rows_with_differing_fields spec)");
    }

    [TestMethod]
    public async Task Insert_ShouldSendRowsWithoutTheirOmittedFields_GivenRowsWithDifferingFields()
    {
        await this.Client.Table<SparseUser>().Insert(RowsWithDifferingFields());
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task Insert_ShouldNotSendColumns_GivenAnEmptyCollection()
    {
        await this.Client.Table<SparseUser>().Insert(new List<SparseUser>());
        this.EmittedRequestQuery.AllKeys.Should().NotContain("columns", "an empty collection writes nothing and must not emit columns=\"\" (spec)");
    }

    [TestMethod]
    public async Task Insert_ShouldNotSendColumns_GivenASingleRow()
    {
        await this.Client.Table<SparseUser>().Insert(new SparseUser { Username = "ann" });
        this.EmittedRequestQuery.AllKeys.Should().NotContain("columns", "only bulk writes name their columns (spec)");
    }

    [TestMethod]
    public async Task Insert_ShouldKeepTheCallersColumns_GivenColumnsWereSpecified()
    {
        await this.Client.Table<SparseUser>().Columns(new[] { "username" }).Insert(RowsWithDifferingFields());
        this.EmittedRequestQuery["columns"].Should().Be("username", "an explicit column list is the caller's choice and wins over the union");
    }
}
