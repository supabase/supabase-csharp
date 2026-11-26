using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Equivalency;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.CreateTable;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.CreateTable;

/// <summary>
/// Contract tests for creating a table: the POST request the catalog puts on the wire (method, path, body, headers),
/// how the table metadata in the response is mapped, and how each failure the Iceberg route can give is surfaced.
/// Failure bodies are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateTableContractTests
{
    private const string TablesPath = "/storage/v1/iceberg/v1/analytics-data/namespaces/sales/tables";
    private static readonly IcebergTable Events = IcebergNamespace.Of("sales").Table("events");
    private static readonly IcebergSchema Schema = new()
    {
        Fields = new[] { new IcebergField { Id = 1, Name = "id", Type = IcebergPrimitiveType.Long, Required = true } },
    };

    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.client = new Client($"{this.server.Url}/storage/v1", new Dictionary<string, string>
        {
            { "Authorization", "Bearer service-key" }
        });
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task CreateTableAsync_ShouldPostToTheTablesRouteOfTheNamespace()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        await this.Catalog().CreateTableAsync(Events, Schema);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST", "the Iceberg route that creates a table is POST /:prefix/namespaces/:namespace/tables");
            request.Path.Should().Be(TablesPath, "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
            request.Headers["Content-Type"].Should().ContainSingle().Which.Should().StartWith("application/json");
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldJoinNamespaceLevelsWithTheUnitSeparator()
    {
        this.server.Given(Request.Create().WithPath("/storage/v1/iceberg/v1/*").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(Fixture("TableCreated.json")));
        await this.Catalog().CreateTableAsync(IcebergNamespace.Of("sales", "europe").Table("events"), Schema);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith("/namespaces/sales%1Feurope/tables", "the Iceberg REST spec joins levels with the 0x1F separator");
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldSendTheNameInTheBody()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        await this.Catalog().CreateTableAsync(Events, Schema);
        this.EmittedBody().RootElement.GetProperty("name").GetString()
            .Should().Be("events", "the namespace travels in the path and the table name in the body");
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldOmitPartitionSpecAndProperties_GivenNoOptions()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        await this.Catalog().CreateTableAsync(Events, Schema, new CreateTableOptions());
        var body = this.EmittedBody().RootElement;
        using (new AssertionScope())
        {
            body.TryGetProperty("partition-spec", out _).Should().BeFalse("the service creates an unpartitioned table without one");
            body.TryGetProperty("properties", out _).Should().BeFalse("an empty properties object adds nothing for the service to store");
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldReturnTheTableMetadata()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        var metadata = await this.Catalog().CreateTableAsync(Events, Schema);
        using (new AssertionScope())
        {
            metadata.MetadataLocation.Should().Be("s3://analytics-data/sales/events/metadata/00000-2eed5277-661d-47b5-84c0-30ce9dbad149.metadata.json");
            metadata.TableUuid.Should().Be(Guid.Parse("b734865e-f9a5-4654-8b0b-e15683fcb195"));
            metadata.FormatVersion.Should().Be(2);
            metadata.Location.Should().Be("s3://analytics-data/sales/events");
            metadata.LastUpdatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1752062419286), "last-updated-ms counts milliseconds since the epoch");
            metadata.LastColumnId.Should().Be(9);
            metadata.LastPartitionId.Should().Be(1001);
            metadata.Properties.Should().Equal(new Dictionary<string, string> { ["write.parquet.compression-codec"] = "zstd" });
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldReturnTheSchemaNamedByTheCurrentSchemaId()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        var metadata = await this.Catalog().CreateTableAsync(Events, Schema);
        metadata.CurrentSchema.Should().BeEquivalentTo(new IcebergSchema
        {
            SchemaId = 1,
            Fields = new[]
            {
                new IcebergField { Id = 1, Name = "id", Type = IcebergPrimitiveType.Long, Required = true, Doc = "The event identifier" },
                new IcebergField { Id = 2, Name = "amount", Type = IcebergPrimitiveType.Decimal(10, 2), Required = false },
                new IcebergField
                {
                    Id = 3, Name = "origin", Required = false,
                    Type = new IcebergStructType { Fields = new[] { new IcebergField { Id = 4, Name = "country", Type = IcebergPrimitiveType.String, Required = false } } },
                },
                new IcebergField
                {
                    Id = 5, Name = "tags", Required = false,
                    Type = new IcebergListType { ElementId = 6, Element = IcebergPrimitiveType.String, ElementRequired = true },
                },
                new IcebergField
                {
                    Id = 7, Name = "attributes", Required = false,
                    Type = new IcebergMapType { KeyId = 8, Key = IcebergPrimitiveType.String, ValueId = 9, Value = IcebergPrimitiveType.Int, ValueRequired = false },
                },
            },
        }, ByStructure<IcebergSchema>(), "current-schema-id picks the schema rows are written with");
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldKeepTheNameOfAnUnknownPrimitiveType()
    {
        this.Respond(200, Fixture("TableCreatedWithUnknownTypes.json"));
        var metadata = await this.Catalog().CreateTableAsync(Events, Schema);
        metadata.CurrentSchema.Fields[0].Type.ToString()
            .Should().Be("variant", "a type the service adds later decodes with the name it sent");
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldKeepTheTagOfAnUnknownNestedType()
    {
        this.Respond(200, Fixture("TableCreatedWithUnknownTypes.json"));
        var metadata = await this.Catalog().CreateTableAsync(Events, Schema);
        metadata.CurrentSchema.Fields[1].Type.ToString()
            .Should().Be("geometry", "a nested type the service adds later decodes with the type it sent");
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldReturnTheSpecNamedByTheDefaultSpecId()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        var metadata = await this.Catalog().CreateTableAsync(Events, Schema);
        metadata.DefaultPartitionSpec.Should().BeEquivalentTo(new IcebergPartitionSpec
        {
            SpecId = 1,
            Fields = new[]
            {
                new IcebergPartitionField { SourceId = 1, FieldId = 1000, Name = "id_bucket", Transform = IcebergTransform.Bucket(16) },
                new IcebergPartitionField { SourceId = 2, FieldId = 1001, Name = "amount_truncate", Transform = IcebergTransform.Truncate(4) },
            },
        }, ByStructure<IcebergPartitionSpec>(), "default-spec-id picks the spec rows are written with");
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldKeepTheNameOfAnUnknownTransform()
    {
        this.Respond(200, Fixture("TableCreatedWithUnknownTypes.json"));
        var metadata = await this.Catalog().CreateTableAsync(Events, Schema);
        metadata.DefaultPartitionSpec.Fields.Should().ContainSingle().Which.Transform.ToString()
            .Should().Be("zorder", "a transform the service adds later decodes with the name it sent");
    }

    [TestMethod]
    [DataRow("TableCreatedWithOnlyRequiredMetadata.json")]
    [DataRow("TableCreatedWithoutCurrentSchemaOrSpec.json")]
    public async Task CreateTableAsync_ShouldReturnEmptySchemaAndSpecWithTheirIds_GivenResponseOmitsThem(string fixture)
    {
        this.Respond(200, Fixture(fixture));
        var metadata = await this.Catalog().CreateTableAsync(Events, Schema);
        using (new AssertionScope())
        {
            metadata.CurrentSchema.Should().BeEquivalentTo(new IcebergSchema { SchemaId = 3, Fields = Array.Empty<IcebergField>() }, ByStructure<IcebergSchema>());
            metadata.DefaultPartitionSpec.Should().BeEquivalentTo(new IcebergPartitionSpec { SpecId = 4, Fields = Array.Empty<IcebergPartitionField>() }, ByStructure<IcebergPartitionSpec>());
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldReturnNoMetadataLocationNorProperties_GivenResponseOmitsThem()
    {
        this.Respond(200, Fixture("TableCreatedWithOnlyRequiredMetadata.json"));
        var metadata = await this.Catalog().CreateTableAsync(Events, Schema);
        using (new AssertionScope())
        {
            metadata.MetadataLocation.Should().BeNull();
            metadata.Properties.Should().BeEmpty();
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldThrow_GivenTypeThatIsNeitherNameNorObject()
    {
        this.Respond(200, Fixture("TableCreatedWithNumericType.json"));
        var act = () => this.Catalog().CreateTableAsync(Events, Schema);
        (await act.Should().ThrowAsync<SupabaseStorageException>("an Iceberg type is a name or a nested type object"))
            .WithInnerException<JsonException>();
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldThrowInvalidInput_GivenInvalidName()
    {
        this.Respond(400, Envelope("Resource name must not contain hyphens (-) or periods (.)", "InvalidParameter", 400));
        var act = () => this.Catalog().CreateTableAsync(IcebergNamespace.Of("sales").Table("my-events"), Schema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(400);
            exception.Code.Should().Be("InvalidParameter", "Storage validates table names with the namespace rules");
        }
    }

    [TestMethod]
    [DataRow("Namespace not found", "NoSuchNamespaceException")]
    [DataRow("Catalog name \"analytics-data\" not found", "NoSuchCatalog")]
    public async Task CreateTableAsync_ShouldThrowNotFound_GivenMissingNamespaceOrBucket(string message, string code)
    {
        this.Respond(404, Envelope(message, code, 404));
        var act = () => this.Catalog().CreateTableAsync(Events, Schema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldThrowAlreadyExists_GivenExistingTable()
    {
        this.Respond(409, Envelope("Table already exists", "AlreadyExistsException", 409));
        var act = () => this.Catalog().CreateTableAsync(Events, Schema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.AlreadyExists);
            exception.Code.Should().Be("AlreadyExistsException", "Storage answers 409 when the namespace already holds the name");
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldThrow_GivenTableLimitReached()
    {
        this.Respond(409, Envelope("The maximum number of this resource (table) 10 is reached", "IcebergMaximumResourceLimit", 409));
        var act = () => this.Catalog().CreateTableAsync(Events, Schema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(409);
            exception.Code.Should().Be("IcebergMaximumResourceLimit");
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond(403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().CreateTableAsync(Events, Schema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.CreateTableAsync(Events, Schema);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task CreateTableAsync_ShouldHonorCancellation()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().CreateTableAsync(Events, Schema, null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void CreateTableAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, Fixture("TableCreated.json"));
        BlockingCaller.Completes(() => this.Catalog().CreateTableAsync(Events, Schema))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static Func<EquivalencyAssertionOptions<T>, EquivalencyAssertionOptions<T>> ByStructure<T>() =>
        options => options.ComparingRecordsByMembers().RespectingRuntimeTypes().WithStrictOrdering()
            .ComparingByValue<IcebergPrimitiveType>().ComparingByValue<IcebergTransform>();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CreateTable", "Data", name));

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private JsonDocument EmittedBody() =>
        JsonDocument.Parse(this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body!);

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(int status, string body) =>
        this.server.Given(Request.Create().WithPath(TablesPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
