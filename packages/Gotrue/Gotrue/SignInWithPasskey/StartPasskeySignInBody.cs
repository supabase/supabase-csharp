using System.Text.Json.Serialization;

namespace Supabase.Gotrue.SignInWithPasskey;

internal sealed record StartPasskeySignInBody
{
    private StartPasskeySignInBody(string captchaToken) => this.Security = new SecurityBody(captchaToken);

    [JsonPropertyName("gotrue_meta_security")]
    public SecurityBody Security { get; }

    internal static StartPasskeySignInBody? For(SignInWithPasskeyOptions? options) =>
        string.IsNullOrEmpty(options?.CaptchaToken) ? null : new StartPasskeySignInBody(options!.CaptchaToken!);

    internal sealed record SecurityBody([property: JsonPropertyName("captcha_token")] string CaptchaToken);
}
