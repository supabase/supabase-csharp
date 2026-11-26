using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.UpdateTable;

internal sealed class UpdateTableBody
{
    private const int LastAdded = -1;

    [JsonPropertyName("requirements")]
    public IReadOnlyList<object> Requirements { get; init; } = null!;

    [JsonPropertyName("updates")]
    public IReadOnlyList<object> Updates { get; init; } = null!;

    internal static UpdateTableBody For(IcebergTableChanges changes) => new()
    {
        Requirements = RequirementsOf(changes).ToList(),
        Updates = UpdatesOf(changes).ToList(),
    };

    private static IEnumerable<object> UpdatesOf(IcebergTableChanges changes)
    {
        if (changes.Schema is { } schema)
        {
            yield return new AddSchemaUpdate(IcebergSchemaBody.From(schema));
            yield return new SetCurrentSchemaUpdate();
        }

        if (changes.PartitionSpec is { } spec)
        {
            yield return new AddSpecUpdate(IcebergPartitionSpecBody.From(spec));
            yield return new SetDefaultSpecUpdate();
        }
    }

    private static IEnumerable<object> RequirementsOf(IcebergTableChanges changes)
    {
        if (changes.BasedOn is not { } basedOn)
            yield break;

        yield return new AssertTableUuid(basedOn.TableUuid);

        if (changes.Schema is not null)
        {
            yield return new AssertCurrentSchemaId(basedOn.CurrentSchema.SchemaId);
            yield return new AssertLastAssignedFieldId(basedOn.LastColumnId);
        }

        if (changes.PartitionSpec is not null)
        {
            yield return new AssertDefaultSpecId(basedOn.DefaultPartitionSpec.SpecId);
            yield return new AssertLastAssignedPartitionId(basedOn.LastPartitionId);
        }
    }

    internal sealed class AddSchemaUpdate(IcebergSchemaBody schema)
    {
        [JsonPropertyName("action")]
        public string Action => "add-schema";

        [JsonPropertyName("schema")]
        public IcebergSchemaBody Schema { get; } = schema;
    }

    internal sealed class SetCurrentSchemaUpdate
    {
        [JsonPropertyName("action")]
        public string Action => "set-current-schema";

        [JsonPropertyName("schema-id")]
        public int SchemaId => LastAdded;
    }

    internal sealed class AddSpecUpdate(IcebergPartitionSpecBody spec)
    {
        [JsonPropertyName("action")]
        public string Action => "add-spec";

        [JsonPropertyName("spec")]
        public IcebergPartitionSpecBody Spec { get; } = spec;
    }

    internal sealed class SetDefaultSpecUpdate
    {
        [JsonPropertyName("action")]
        public string Action => "set-default-spec";

        [JsonPropertyName("spec-id")]
        public int SpecId => LastAdded;
    }

    internal sealed class AssertTableUuid(Guid uuid)
    {
        [JsonPropertyName("type")]
        public string Type => "assert-table-uuid";

        [JsonPropertyName("uuid")]
        public Guid Uuid { get; } = uuid;
    }

    internal sealed class AssertCurrentSchemaId(int? currentSchemaId)
    {
        [JsonPropertyName("type")]
        public string Type => "assert-current-schema-id";

        [JsonPropertyName("current-schema-id")]
        public int? CurrentSchemaId { get; } = currentSchemaId;
    }

    internal sealed class AssertLastAssignedFieldId(int lastAssignedFieldId)
    {
        [JsonPropertyName("type")]
        public string Type => "assert-last-assigned-field-id";

        [JsonPropertyName("last-assigned-field-id")]
        public int LastAssignedFieldId { get; } = lastAssignedFieldId;
    }

    internal sealed class AssertDefaultSpecId(int? defaultSpecId)
    {
        [JsonPropertyName("type")]
        public string Type => "assert-default-spec-id";

        [JsonPropertyName("default-spec-id")]
        public int? DefaultSpecId { get; } = defaultSpecId;
    }

    internal sealed class AssertLastAssignedPartitionId(int lastAssignedPartitionId)
    {
        [JsonPropertyName("type")]
        public string Type => "assert-last-assigned-partition-id";

        [JsonPropertyName("last-assigned-partition-id")]
        public int LastAssignedPartitionId { get; } = lastAssignedPartitionId;
    }
}
