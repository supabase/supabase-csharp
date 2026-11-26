using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.UpdateTable;

namespace Storage.Tests.UpdateTable;

/// <summary>
/// Pins the exact bytes <c>UpdateTableAsync</c> puts on the wire; the approved bodies must match the Iceberg REST
/// <c>CommitTableRequest</c> that Storage forwards from <c>POST /iceberg/v1/:prefix/namespaces/:namespace/tables/:table</c>
/// (<c>requirements</c> and <c>updates</c> both required; <c>-1</c> names the schema or spec added by the same commit).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateTableApprovalTests : RequestApprovalFixture
{
    private static readonly IcebergTable Events = IcebergNamespace.Of("sales").Table("events");

    private static readonly IcebergSchema Evolved = new()
    {
        Fields = new[]
        {
            new IcebergField { Id = 1, Name = "id", Type = IcebergPrimitiveType.Long, Required = true },
            new IcebergField { Id = 3, Name = "country", Type = IcebergPrimitiveType.String, Required = false },
        },
    };

    private static readonly IcebergPartitionSpec ByCountry = new()
    {
        Fields = new[] { new IcebergPartitionField { SourceId = 3, Name = "country", Transform = IcebergTransform.Identity } },
    };

    private static readonly IcebergTableMetadata Current = new()
    {
        TableUuid = Guid.Parse("b734865e-f9a5-4654-8b0b-e15683fcb195"),
        FormatVersion = 2,
        Location = "s3://analytics-data/sales/events",
        LastUpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(1752062419286),
        CurrentSchema = new IcebergSchema { SchemaId = 4, Fields = Array.Empty<IcebergField>() },
        DefaultPartitionSpec = new IcebergPartitionSpec { SpecId = 5, Fields = Array.Empty<IcebergPartitionField>() },
        LastColumnId = 2,
        LastPartitionId = 1000,
        Properties = new Dictionary<string, string>(),
    };

    [TestMethod]
    public async Task UpdateTableRequest_ShouldSerializeSchemaChange()
    {
        this.RespondWith(Updated());
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges { Schema = Evolved });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateTableRequest_ShouldSerializePartitionSpecChange()
    {
        this.RespondWith(Updated());
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges { PartitionSpec = ByCountry });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateTableRequest_ShouldSerializeRequirementsOfSchemaChange_GivenBasedOn()
    {
        this.RespondWith(Updated());
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges { Schema = Evolved, BasedOn = Current });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateTableRequest_ShouldSerializeRequirementsOfPartitionSpecChange_GivenBasedOn()
    {
        this.RespondWith(Updated());
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges { PartitionSpec = ByCountry, BasedOn = Current });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateTableRequest_ShouldSerializeEmptyCommit_GivenNoChange()
    {
        this.RespondWith(Updated());
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges());
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    private static string Updated() =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateTable", "Data", "TableUpdated.json"));
}
