using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Claims;

/// <summary>
/// Reads the "aud" claim, which RFC 7519 §4.1.3 allows to be one string or an array of them.
/// </summary>
internal sealed class AudienceConverter : JsonConverter<IReadOnlyList<string>>
{
    public override bool HandleNull => true;

    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var audiences = reader.TokenType == JsonTokenType.String
            ? new[] { reader.GetString()! }
            : JsonSerializer.Deserialize<string[]>(ref reader, options);
        if (audiences == null || Array.IndexOf(audiences, null) >= 0)
        {
            throw new JsonException("aud must be a string or an array of strings.");
        }
        return audiences;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}
