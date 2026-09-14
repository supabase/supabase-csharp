using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Postgrest.Converters;

/// <summary>
/// Reads a <c>List&lt;int&gt;</c> from a JSON array (<c>[1,2,3]</c>) or a Postgres array literal
/// (<c>"{1,2,3}"</c>), and writes a JSON array.
/// </summary>
public class IntArrayConverter : JsonConverter<List<int>>
{
    /// <inheritdoc />
    public override List<int>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return ParseLiteral(reader.GetString()!);
            case JsonTokenType.StartArray:
                var list = new List<int>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    list.Add(reader.GetInt32());
                return list;
            default:
                throw new JsonException($"Unexpected token {reader.TokenType} when reading List<int>.");
        }
    }

    private static List<int> ParseLiteral(string literal)
    {
        var contents = literal.Trim('{', '}');
        if (contents.Length == 0)
            return new List<int>();

        var result = new List<int>();
        foreach (var part in contents.Split(','))
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var item))
                throw new JsonException($"Cannot read '{literal}' as List<int>.");

            result.Add(item);
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, List<int> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteNumberValue(item);
        writer.WriteEndArray();
    }
}
