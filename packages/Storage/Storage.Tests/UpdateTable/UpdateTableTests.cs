using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.UpdateTable;

namespace Storage.Tests.UpdateTable;

/// <summary>
/// Unit tests for the checks made before the wire when updating a table, and for the defaults of its changes.
/// The changes themselves are validated by the catalog.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class UpdateTableTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task UpdateTableAsync_ShouldThrowArgumentNullException_GivenNoTable() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").UpdateTableAsync(null!, new IcebergTableChanges()))
            .Should().ThrowAsync<ArgumentNullException>("there is no table to update"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    public async Task UpdateTableAsync_ShouldThrowArgumentNullException_GivenNoChanges() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").UpdateTableAsync(IcebergNamespace.Of("sales").Table("events"), null!))
            .Should().ThrowAsync<ArgumentNullException>("there are no changes to commit"))
            .Which.ParamName.Should().Be("changes");

    [TestMethod]
    [DataRow("")]
    [DataRow("..")]
    public async Task UpdateTableAsync_ShouldThrowArgumentException_GivenNameThatChangesTheRoute(string name) =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").UpdateTableAsync(IcebergNamespace.Of("sales").Table(name), new IcebergTableChanges()))
            .Should().ThrowAsync<ArgumentException>("a blank or dot-segment name would post to another route"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    public void IcebergTableChanges_ShouldChangeNothing_GivenNoneSet()
    {
        var changes = new IcebergTableChanges();
        using (new AssertionScope())
        {
            changes.Schema.Should().BeNull();
            changes.PartitionSpec.Should().BeNull();
            changes.BasedOn.Should().BeNull("without it the commit asserts nothing about the table");
        }
    }
}
