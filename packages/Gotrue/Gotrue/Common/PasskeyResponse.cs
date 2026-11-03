using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.Common;

internal sealed record PasskeyResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("last_used_at")]
    public DateTimeOffset? LastUsedAt { get; init; }

    internal static IReadOnlyList<Passkey> Read(IReadOnlyList<PasskeyResponse>? response) =>
        (response ?? Array.Empty<PasskeyResponse>()).Select(passkey => passkey.ToPasskey()).ToList();

    internal static Passkey Read(PasskeyResponse? response) =>
        response?.ToPasskey() ?? throw new GotrueException("The auth server returned no passkey.");

    private Passkey ToPasskey() =>
        new()
        {
            Id = this.Id,
            FriendlyName = this.FriendlyName,
            CreatedAt = this.CreatedAt,
            LastUsedAt = this.LastUsedAt,
        };
}
