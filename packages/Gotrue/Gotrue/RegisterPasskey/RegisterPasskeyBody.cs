using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.RegisterPasskey;

internal sealed record RegisterPasskeyBody
{
    private RegisterPasskeyBody(Guid challengeId, JsonElement credential)
    {
        this.ChallengeId = challengeId;
        this.Credential = credential;
    }

    [JsonPropertyName("challenge_id")]
    public Guid ChallengeId { get; }

    [JsonPropertyName("credential")]
    public JsonElement Credential { get; }

    internal static RegisterPasskeyBody For(Guid challengeId, JsonElement credential) =>
        credential.ValueKind == JsonValueKind.Object
            ? new RegisterPasskeyBody(challengeId, credential)
            : throw new ArgumentException("The passkey credential must be a JSON object.", nameof(credential));
}
