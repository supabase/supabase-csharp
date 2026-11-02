using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.Common;

internal sealed record RecoveryCodesResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("friendly_name")]
    public string FriendlyName { get; init; } = null!;

    [JsonPropertyName("codes")]
    public IReadOnlyList<string>? Codes { get; init; }

    internal static RecoveryCodes Read(RecoveryCodesResponse? response) =>
        response?.ToRecoveryCodes() ?? throw new GotrueException("The auth server returned no recovery codes.");

    private RecoveryCodes ToRecoveryCodes() =>
        new()
        {
            FactorId = this.Id,
            FriendlyName = this.FriendlyName,
            Codes = this.Codes ?? Array.Empty<string>(),
        };
}
