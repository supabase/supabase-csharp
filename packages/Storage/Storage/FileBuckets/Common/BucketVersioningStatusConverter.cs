using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Storage.FileBuckets.Common;

internal sealed class BucketVersioningStatusConverter : JsonConverter<BucketVersioningStatus>
{
    public override BucketVersioningStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        BucketVersioningStatus.FromWire(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, BucketVersioningStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
