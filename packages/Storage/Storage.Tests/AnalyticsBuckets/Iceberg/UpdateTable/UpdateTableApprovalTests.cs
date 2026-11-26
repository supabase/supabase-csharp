using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;
using Supabase.Storage.AnalyticsBuckets.Iceberg.UpdateTable;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.UpdateTable;

/// <summary>
/// Pins the exact bytes <c>UpdateTableAsync</c> puts on the wire; the approved bodies must match the Iceberg REST
/// <c>CommitTableRequest</c> that Storage forwards from <c>POST /iceberg/v1/:prefix/namespaces/:namespace/tables/:table</c>
/// (<c>requirements</c> and <c>updates</c> both required; <c>-1</c> names the schema or spec added by the same commit;
/// <c>schema-id</c> and <c>spec-id</c> read-only, so the catalog assigns them).
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
        var current = await this.LoadEvents();
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges { Schema = Evolved, BasedOn = current });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateTableRequest_ShouldSerializeRequirementsOfPartitionSpecChange_GivenBasedOn()
    {
        var current = await this.LoadEvents();
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges { PartitionSpec = ByCountry, BasedOn = current });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateTableRequest_ShouldLeaveTheSchemaIdToTheCatalog_GivenLoadedSchema()
    {
        var current = await this.LoadEvents();
        var country = new IcebergField { Id = current.LastColumnId + 1, Name = "country", Type = IcebergPrimitiveType.String, Required = false };
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges
        {
            Schema = current.CurrentSchema with { Fields = current.CurrentSchema.Fields.Append(country).ToList() },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateTableRequest_ShouldLeaveTheSpecIdToTheCatalogAndKeepFieldIds_GivenLoadedSpec()
    {
        var current = await this.LoadEvents();
        var byId = new IcebergPartitionField { SourceId = 1, Name = "id_bucket", Transform = IcebergTransform.Bucket(16) };
        await this.Client.Analytics.From("analytics-data").UpdateTableAsync(Events, new IcebergTableChanges
        {
            PartitionSpec = current.DefaultPartitionSpec with { Fields = current.DefaultPartitionSpec.Fields.Append(byId).ToList() },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    private async Task<IcebergTableMetadata> LoadEvents()
    {
        this.RespondWith(Fixture("TableToUpdate.json"));
        var current = await this.Client.Analytics.From("analytics-data").LoadTableAsync(Events);
        this.ForgetEmittedRequests();
        return current;
    }

    private static string Updated() => Fixture("TableUpdated.json");

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AnalyticsBuckets", "Iceberg", "UpdateTable", "Data", name));
}
