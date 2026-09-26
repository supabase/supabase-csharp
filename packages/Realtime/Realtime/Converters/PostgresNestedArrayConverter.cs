using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Realtime.Converters;

/// <summary>
/// Reads a nested Postgres array such as <c>{{1,2},{3,4}}</c> from a string into a <see cref="List{T}" /> of
/// lists of int or string. A regular JSON array is also accepted; writes emit a regular JSON array.
/// </summary>
internal class PostgresNestedArrayConverter : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        if (!IsList(typeToConvert) || !IsList(typeToConvert.GetGenericArguments()[0]))
            return false;
        var leaf = Leaf(typeToConvert);
        return leaf == typeof(int) || leaf == typeof(string);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter) Activator.CreateInstance(
            typeof(NestedConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private static bool IsList(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

    private static Type Leaf(Type type) =>
        IsList(type) ? Leaf(type.GetGenericArguments()[0]) : type;

    private static object? Materialize(object? element, Type type)
    {
        if (IsList(type))
        {
            if (element is not List<object?> children)
                throw new JsonException($"Expected a nested array for {type}.");

            var list = (IList) Activator.CreateInstance(type)!;
            foreach (var child in children)
                list.Add(Materialize(child, type.GetGenericArguments()[0]));
            return list;
        }

        if (element is List<object?>)
            throw new JsonException($"Expected a single value for {type}.");

        if (type == typeof(int))
            return int.Parse((string) element!, CultureInfo.InvariantCulture);

        return element;
    }

    private class NestedConverter<TElement> : JsonConverter<List<TElement>>
    {
        public override List<TElement>? Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            var start = reader;
            try
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Null:
                        return null;
                    case JsonTokenType.String:
                        var literal = PostgresArrayLiteral.Parse(reader.GetString()!);
                        return (List<TElement>) Materialize(literal, typeof(List<TElement>))!;
                    case JsonTokenType.StartArray:
                        var list = new List<TElement>();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            list.Add(JsonSerializer.Deserialize<TElement>(ref reader, options) ?? throw new JsonException());
                        return list;
                    default:
                        reader.Skip();
                        return null;
                }
            }
            catch
            {
                // A JSON array may be half read; rewind and skip it so the rest of the payload still parses.
                reader = start;
                reader.Skip();
                return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, List<TElement> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value)
                JsonSerializer.Serialize(writer, item, options);
            writer.WriteEndArray();
        }
    }
}
