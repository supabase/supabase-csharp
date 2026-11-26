using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;
using Supabase.Storage.AnalyticsBuckets.Iceberg.CreateTable;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.CreateTable;

/// <summary>
/// Pins the exact bytes <c>CreateTableAsync</c> puts on the wire; the approved bodies must match the Iceberg REST
/// <c>CreateTableRequest</c> that Storage forwards from <c>POST /iceberg/v1/:prefix/namespaces/:namespace/tables</c>
/// (<c>name</c> and <c>schema</c> required; <c>partition-spec</c> and <c>properties</c> optional; <c>schema-id</c> and
/// <c>spec-id</c> read-only, so the catalog assigns them).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateTableApprovalTests : RequestApprovalFixture
{
    private static readonly IcebergTable Events = IcebergNamespace.Of("sales").Table("events");

    [TestMethod]
    public async Task CreateTableRequest_ShouldSerializeToExpectedPayload()
    {
        this.RespondWith(Created());
        await this.Client.Analytics.From("analytics-data").CreateTableAsync(
            Events,
            new IcebergSchema { Fields = new[] { new IcebergField { Id = 1, Name = "id", Type = IcebergPrimitiveType.Long, Required = true } } });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateTableRequest_ShouldSerializeEveryPrimitiveType()
    {
        this.RespondWith(Created());
        await this.Client.Analytics.From("analytics-data").CreateTableAsync(Events, new IcebergSchema { Fields = PrimitiveFields() });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateTableRequest_ShouldSerializeNestedTypesAndDocs()
    {
        this.RespondWith(Created());
        await this.Client.Analytics.From("analytics-data").CreateTableAsync(Events, new IcebergSchema
        {
            Fields = new[]
            {
                new IcebergField
                {
                    Id = 1, Name = "origin", Required = false, Doc = "Where the event came from",
                    Type = new IcebergStructType { Fields = new[] { new IcebergField { Id = 2, Name = "country", Type = IcebergPrimitiveType.String, Required = true } } },
                },
                new IcebergField
                {
                    Id = 3, Name = "tags", Required = false,
                    Type = new IcebergListType { ElementId = 4, Element = IcebergPrimitiveType.String, ElementRequired = true },
                },
                new IcebergField
                {
                    Id = 5, Name = "attributes", Required = true,
                    Type = new IcebergMapType
                    {
                        KeyId = 6, Key = IcebergPrimitiveType.String, ValueId = 7, ValueRequired = false,
                        Value = new IcebergListType { ElementId = 8, Element = IcebergPrimitiveType.Int, ElementRequired = false },
                    },
                },
            },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateTableRequest_ShouldSerializePartitionSpecAndProperties_GivenOptions()
    {
        this.RespondWith(Created());
        await this.Client.Analytics.From("analytics-data").CreateTableAsync(
            Events,
            new IcebergSchema { Fields = new[] { new IcebergField { Id = 1, Name = "id", Type = IcebergPrimitiveType.Long, Required = true } } },
            new CreateTableOptions
            {
                PartitionSpec = new IcebergPartitionSpec
                {
                    Fields = new[]
                    {
                        new IcebergPartitionField { SourceId = 1, Name = "id_identity", Transform = IcebergTransform.Identity, FieldId = 1000 },
                        new IcebergPartitionField { SourceId = 1, Name = "id_bucket", Transform = IcebergTransform.Bucket(16) },
                        new IcebergPartitionField { SourceId = 1, Name = "id_truncate", Transform = IcebergTransform.Truncate(10) },
                        new IcebergPartitionField { SourceId = 2, Name = "at_year", Transform = IcebergTransform.Year },
                        new IcebergPartitionField { SourceId = 2, Name = "at_month", Transform = IcebergTransform.Month },
                        new IcebergPartitionField { SourceId = 2, Name = "at_day", Transform = IcebergTransform.Day },
                        new IcebergPartitionField { SourceId = 2, Name = "at_hour", Transform = IcebergTransform.Hour },
                        new IcebergPartitionField { SourceId = 2, Name = "at_void", Transform = IcebergTransform.Void },
                    },
                },
                Properties = new Dictionary<string, string> { ["write.format.default"] = "parquet" },
            });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateTableRequest_ShouldLeaveSchemaAndSpecIdsToTheCatalog_GivenLoadedSchemaAndSpec()
    {
        this.RespondWith(Created());
        var loaded = await this.Client.Analytics.From("analytics-data").LoadTableAsync(Events);
        this.ForgetEmittedRequests();
        await this.Client.Analytics.From("analytics-data").CreateTableAsync(
            IcebergNamespace.Of("sales").Table("events_copy"),
            loaded.CurrentSchema,
            new CreateTableOptions { PartitionSpec = loaded.DefaultPartitionSpec });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    private static IReadOnlyList<IcebergField> PrimitiveFields()
    {
        var types = new[]
        {
            IcebergPrimitiveType.Boolean, IcebergPrimitiveType.Int, IcebergPrimitiveType.Long, IcebergPrimitiveType.Float,
            IcebergPrimitiveType.Double, IcebergPrimitiveType.Decimal(10, 2), IcebergPrimitiveType.Date, IcebergPrimitiveType.Time,
            IcebergPrimitiveType.Timestamp, IcebergPrimitiveType.TimestampTz, IcebergPrimitiveType.String, IcebergPrimitiveType.Uuid,
            IcebergPrimitiveType.Fixed(16), IcebergPrimitiveType.Binary,
        };
        return types.Select((type, index) => new IcebergField { Id = index + 1, Name = $"column_{index + 1}", Type = type, Required = false }).ToList();
    }

    private static string Created() =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AnalyticsBuckets", "Iceberg", "CreateTable", "Data", "TableCreated.json"));
}
