using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.AuthorizationOAuth;

internal sealed class OAuthAuthorizationResponseConverter : JsonConverter<OAuthAuthorizationResponse>
{
    public override OAuthAuthorizationResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The OAuth authorization response must be a JSON object.");
        }

        if (root.TryGetProperty("redirect_url", out _))
        {
            return new OAuthAuthorizationResponse
            {
                Redirect = JsonSerializer.Deserialize<OAuthRedirect>(root.GetRawText(), options),
            };
        }

        return new OAuthAuthorizationResponse
        {
            Details = JsonSerializer.Deserialize<OAuthAuthorizationDetails>(root.GetRawText(), options),
        };
    }

    public override void Write(Utf8JsonWriter writer, OAuthAuthorizationResponse value, JsonSerializerOptions options)
    {
        if (value.Details is not null)
        {
            JsonSerializer.Serialize(writer, value.Details, options);
            return;
        }

        if (value.Redirect is not null)
        {
            JsonSerializer.Serialize(writer, value.Redirect, options);
            return;
        }

        throw new JsonException("The OAuth authorization response must contain details or a redirect.");
    }
}
