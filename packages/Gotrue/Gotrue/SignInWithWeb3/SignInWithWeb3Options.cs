namespace Supabase.Gotrue.SignInWithWeb3;

/// <summary>
/// Optional settings for a Web3 sign-in.
/// </summary>
public sealed record SignInWithWeb3Options
{
    /// <summary>The CAPTCHA token, required when CAPTCHA protection is enabled on the project.</summary>
    public string? CaptchaToken { get; init; }
}
