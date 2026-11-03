namespace Supabase.Gotrue.SignInWithPasskey;

/// <summary>
/// Optional settings for starting a passkey sign-in.
/// </summary>
public sealed record SignInWithPasskeyOptions
{
    /// <summary>The CAPTCHA token, required when CAPTCHA protection is enabled on the project.</summary>
    public string? CaptchaToken { get; init; }
}
