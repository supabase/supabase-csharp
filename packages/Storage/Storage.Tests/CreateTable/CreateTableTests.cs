using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.CreateTable;

namespace Storage.Tests.CreateTable;

/// <summary>
/// Unit tests for the checks made before the wire when creating a table, and for the defaults of its options.
/// The name and the schema themselves are validated by the service.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class CreateTableTests
{
    private static readonly IcebergTable Events = IcebergTable.Of(IcebergNamespace.Of("sales"), "events");
    private static readonly IcebergSchema Schema = new() { Fields = Array.Empty<IcebergField>() };
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task CreateTableAsync_ShouldThrowArgumentNullException_GivenNoTable() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").CreateTableAsync(null!, Schema))
            .Should().ThrowAsync<ArgumentNullException>("there is no table to create"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    public async Task CreateTableAsync_ShouldThrowArgumentNullException_GivenNoSchema() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").CreateTableAsync(Events, null!))
            .Should().ThrowAsync<ArgumentNullException>("a table cannot be created without its columns"))
            .Which.ParamName.Should().Be("schema");

    [TestMethod]
    public async Task CreateTableAsync_ShouldThrowArgumentException_GivenRelativeSegmentNamespace() =>
        await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").CreateTableAsync(IcebergTable.Of(IcebergNamespace.Of(".."), "events"), Schema))
            .Should().ThrowAsync<ArgumentException>("a dot-segment level would reach another route")
            .WithMessage("A namespace cannot be a relative path segment.*");

    [TestMethod]
    public void CreateTableOptions_ShouldHaveNoProperties_GivenNoneSet() =>
        new CreateTableOptions().Properties.Should().BeEmpty();

    [TestMethod]
    public void CreateTableOptions_ShouldHaveNoPartitionSpec_GivenNoneSet() =>
        new CreateTableOptions().PartitionSpec.Should().BeNull("the service owns the default partitioning");
}
