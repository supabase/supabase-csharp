using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.ListPasskeys;

internal sealed record PasskeyResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("last_used_at")]
    public DateTime? LastUsedAt { get; init; }

    internal static IReadOnlyList<Passkey> Read(IReadOnlyList<PasskeyResponse>? response) =>
        (response ?? Array.Empty<PasskeyResponse>()).Select(passkey => passkey.ToPasskey()).ToList();

    private Passkey ToPasskey() =>
        new()
        {
            Id = this.Id,
            FriendlyName = this.FriendlyName,
            CreatedAt = this.CreatedAt,
            LastUsedAt = this.LastUsedAt,
        };
}
