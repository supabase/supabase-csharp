using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.SignInWithPasskey;

internal sealed record PasskeySignInChallengeResponse
{
    [JsonPropertyName("challenge_id")]
    public Guid ChallengeId { get; init; }

    [JsonPropertyName("options")]
    public JsonElement Options { get; init; }

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; init; }

    internal static PasskeySignInChallenge Read(PasskeySignInChallengeResponse? response) =>
        response?.ToChallenge() ?? throw new GotrueException("The auth server returned no passkey sign-in challenge.");

    private PasskeySignInChallenge ToChallenge() =>
        new()
        {
            Id = this.ChallengeId,
            Options = this.Options,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(this.ExpiresAt),
        };
}
