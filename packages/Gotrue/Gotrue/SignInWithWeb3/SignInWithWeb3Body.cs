using System.Text.Json.Serialization;

namespace Supabase.Gotrue.SignInWithWeb3;

internal sealed record SignInWithWeb3Body
{
    private SignInWithWeb3Body(Web3Credentials credentials, SecurityBody? security)
    {
        this.Chain = credentials.Chain;
        this.Message = credentials.Message;
        this.Signature = credentials.Signature;
        this.Security = security;
    }

    [JsonPropertyName("chain")]
    public string Chain { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("signature")]
    public string Signature { get; }

    [JsonPropertyName("gotrue_meta_security")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SecurityBody? Security { get; }

    internal static SignInWithWeb3Body For(Web3Credentials credentials, SignInWithWeb3Options? options) =>
        new(credentials, SecurityFor(options));

    private static SecurityBody? SecurityFor(SignInWithWeb3Options? options) =>
        string.IsNullOrEmpty(options?.CaptchaToken) ? null : new SecurityBody(options!.CaptchaToken!);

    internal sealed record SecurityBody([property: JsonPropertyName("captcha_token")] string CaptchaToken);
}
