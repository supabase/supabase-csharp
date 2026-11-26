using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Storage.Common;

internal sealed class IcebergTypeConverter : JsonConverter<IcebergType>
{
    public override IcebergType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);
        return element.ValueKind == JsonValueKind.String
            ? IcebergPrimitiveType.FromWire(element.GetString()!)
            : NestedTypeOf(element, options);
    }

    public override void Write(Utf8JsonWriter writer, IcebergType value, JsonSerializerOptions options)
    {
        var wireShape = WireShapeOf(value);
        JsonSerializer.Serialize(writer, wireShape, wireShape.GetType(), options);
    }

    private static IcebergType NestedTypeOf(JsonElement element, JsonSerializerOptions options) =>
        element.Deserialize<TypeTagBody>(options)!.Type switch
        {
            StructTypeBody.Tag => element.Deserialize<StructTypeBody>(options)!.ToType(),
            ListTypeBody.Tag => element.Deserialize<ListTypeBody>(options)!.ToType(),
            MapTypeBody.Tag => element.Deserialize<MapTypeBody>(options)!.ToType(),
            var unknown => IcebergPrimitiveType.FromWire(unknown),
        };

    private static object WireShapeOf(IcebergType type) => type switch
    {
        IcebergStructType structType => StructTypeBody.From(structType),
        IcebergListType listType => ListTypeBody.From(listType),
        IcebergMapType mapType => MapTypeBody.From(mapType),
        _ => type.ToString(),
    };

    internal sealed class TypeTagBody
    {
        [JsonRequired]
        [JsonPropertyName("type")]
        public string Type { get; init; } = null!;
    }

    internal sealed class StructTypeBody
    {
        internal const string Tag = "struct";

        [JsonPropertyName("type")]
        public string Type { get; init; } = Tag;

        [JsonRequired]
        [JsonPropertyName("fields")]
        public IReadOnlyList<IcebergFieldBody> Fields { get; init; } = null!;

        internal static StructTypeBody From(IcebergStructType type) => new() { Fields = IcebergFieldBody.FromAll(type.Fields) };

        internal IcebergStructType ToType() => new() { Fields = IcebergFieldBody.ToFields(this.Fields) };
    }

    internal sealed class ListTypeBody
    {
        internal const string Tag = "list";

        [JsonPropertyName("type")]
        public string Type { get; init; } = Tag;

        [JsonRequired]
        [JsonPropertyName("element-id")]
        public int ElementId { get; init; }

        [JsonRequired]
        [JsonPropertyName("element")]
        [JsonConverter(typeof(IcebergTypeConverter))]
        public IcebergType Element { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("element-required")]
        public bool ElementRequired { get; init; }

        internal static ListTypeBody From(IcebergListType type) => new()
        {
            ElementId = type.ElementId,
            Element = type.Element,
            ElementRequired = type.ElementRequired,
        };

        internal IcebergListType ToType() => new()
        {
            ElementId = this.ElementId,
            Element = this.Element,
            ElementRequired = this.ElementRequired,
        };
    }

    internal sealed class MapTypeBody
    {
        internal const string Tag = "map";

        [JsonPropertyName("type")]
        public string Type { get; init; } = Tag;

        [JsonRequired]
        [JsonPropertyName("key-id")]
        public int KeyId { get; init; }

        [JsonRequired]
        [JsonPropertyName("key")]
        [JsonConverter(typeof(IcebergTypeConverter))]
        public IcebergType Key { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("value-id")]
        public int ValueId { get; init; }

        [JsonRequired]
        [JsonPropertyName("value")]
        [JsonConverter(typeof(IcebergTypeConverter))]
        public IcebergType Value { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("value-required")]
        public bool ValueRequired { get; init; }

        internal static MapTypeBody From(IcebergMapType type) => new()
        {
            KeyId = type.KeyId,
            Key = type.Key,
            ValueId = type.ValueId,
            Value = type.Value,
            ValueRequired = type.ValueRequired,
        };

        internal IcebergMapType ToType() => new()
        {
            KeyId = this.KeyId,
            Key = this.Key,
            ValueId = this.ValueId,
            Value = this.Value,
            ValueRequired = this.ValueRequired,
        };
    }
}
