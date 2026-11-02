using System.Text.Json.Serialization;

namespace Supabase.Gotrue.VerifyRecoveryCode;

internal sealed record VerifyRecoveryCodeBody
{
    internal VerifyRecoveryCodeBody(string code) => this.Code = code;

    [JsonPropertyName("code")]
    public string Code { get; }
}
