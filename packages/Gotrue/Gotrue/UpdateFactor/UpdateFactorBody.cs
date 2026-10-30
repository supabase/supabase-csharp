using System;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.UpdateFactor;

internal sealed record UpdateFactorBody
{
    private UpdateFactorBody(FactorChanges changes)
    {
        this.FriendlyName = changes.FriendlyName;
        this.Phone = changes.Phone;
    }

    [JsonPropertyName("friendly_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FriendlyName { get; }

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; }

    internal static UpdateFactorBody For(FactorChanges changes) =>
        new(changes ?? throw new ArgumentNullException(nameof(changes)));
}
