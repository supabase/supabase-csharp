using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Supabase.Realtime.Converters;

/// <summary>
/// Parses a Postgres array literal such as <c>{a,"b,c",NULL}</c>. Values come back as strings, <c>NULL</c> as null
/// and inner arrays as nested lists. Malformed input throws <see cref="JsonException" />.
/// </summary>
internal static class PostgresArrayLiteral
{
    // Postgres treats only these six characters as whitespace inside a literal.
    private static readonly char[] Spaces = { ' ', '\t', '\n', '\r', '\v', '\f' };

    internal static List<object?> Parse(string literal)
    {
        var text = literal.Trim(Spaces);
        if (text.Length < 2 || (text[0] != '{' && text[0] != '['))
        {
            throw Malformed(text);
        }

        // [0:1]={1,2} has custom bounds, [1,2,3] is the old bracket form.
        if (text[0] == '[' && text[text.Length - 1] == '}')
        {
            throw new JsonException($"Array bounds are not supported: '{text}'.");
        }

        var close = text[0] == '[' ? ']' : '}';
        var position = 1;
        var elements = ReadElements(text, close, ref position, 1);
        if (position != text.Length)
        {
            throw Malformed(text);
        }

        return elements;
    }

    private static List<object?> ReadElements(string text, char close, ref int position, int depth)
    {
        var elements = new List<object?>();
        SkipWhitespace(text, ref position);
        if (position < text.Length && text[position] == close)
        {
            position++;
            return elements;
        }

        while (position < text.Length)
        {
            elements.Add(text[position] switch
            {
                '{' => ReadNested(text, ref position, depth),
                '"' => ReadQuoted(text, ref position),
                _ => ReadUnquoted(text, ref position, close),
            });

            SkipWhitespace(text, ref position);
            if (position < text.Length && text[position] == close)
            {
                position++;
                return elements;
            }

            if (position >= text.Length || text[position] != ',')
            {
                throw Malformed(text);
            }

            position++;
            SkipWhitespace(text, ref position);
        }

        throw Malformed(text);
    }

    private static List<object?> ReadNested(string text, ref int position, int depth)
    {
        // Postgres arrays have at most six dimensions.
        if (depth == 6)
        {
            throw Malformed(text);
        }

        position++;
        return ReadElements(text, '}', ref position, depth + 1);
    }

    private static string ReadQuoted(string text, ref int position)
    {
        var element = new StringBuilder();
        position++;
        while (position < text.Length)
        {
            var character = text[position++];
            if (character == '"')
            {
                return element.ToString();
            }

            if (character == '\\' && position < text.Length)
            {
                character = text[position++];
            }

            element.Append(character);
        }

        throw Malformed(text);
    }

    private static string? ReadUnquoted(string text, ref int position, char close)
    {
        var start = position;
        while (position < text.Length && text[position] != ',' && text[position] != close)
        {
            // Postgres quotes any element that contains a quote, brace or backslash, so a bare one is malformed.
            if (text[position] == '"' || text[position] == '{' || text[position] == '\\')
            {
                throw Malformed(text);
            }

            position++;
        }

        var value = text.Substring(start, position - start).TrimEnd(Spaces);
        if (value.Length == 0)
        {
            throw Malformed(text);
        }

        return value.Equals("NULL", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && Array.IndexOf(Spaces, text[position]) >= 0)
        {
            position++;
        }
    }

    private static JsonException Malformed(string text) =>
        new($"Malformed Postgres array literal '{text}'.");
}
