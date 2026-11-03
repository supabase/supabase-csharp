using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.SignInWithPasskey;

internal sealed record SignInWithPasskeyBody
{
    private SignInWithPasskeyBody(Guid challengeId, JsonElement credential)
    {
        this.ChallengeId = challengeId;
        this.Credential = credential;
    }

    [JsonPropertyName("challenge_id")]
    public Guid ChallengeId { get; }

    [JsonPropertyName("credential")]
    public JsonElement Credential { get; }

    internal static SignInWithPasskeyBody For(Guid challengeId, JsonElement credential) =>
        credential.ValueKind == JsonValueKind.Object
            ? new SignInWithPasskeyBody(challengeId, credential)
            : throw new ArgumentException("The passkey assertion must be a JSON object.", nameof(credential));
}
