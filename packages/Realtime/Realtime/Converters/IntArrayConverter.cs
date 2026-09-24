using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: InternalsVisibleTo("Realtime.Tests")]

namespace Supabase.Realtime.Converters;

/// <summary>
/// An int array converter that specifically parses Postgrest styled arrays `{1,2,3}` and `[1,2,3]` from strings
/// into a <see cref="List{T}"/>. A regular JSON array is also accepted; writes emit a regular JSON array.
/// </summary>
public class IntArrayConverter : JsonConverter<List<int>>
{
    /// <inheritdoc />
    public override List<int>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String:
                    return Parse(reader.GetString()!);
                case JsonTokenType.StartArray:
                    var list = new List<int>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        list.Add(reader.GetInt32());
                    return list;
                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, List<int> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteNumberValue(item);
        writer.WriteEndArray();
    }

    internal static List<int> Parse(string value)
    {
        var result = new List<int>();
        foreach (var item in PostgresArrayLiteral.Parse(value))
        {
            if (item is not string element)
                throw new JsonException($"'{value}' is not a flat array of ints.");
            result.Add(int.Parse(element, CultureInfo.InvariantCulture));
        }

        return result;
    }
}
