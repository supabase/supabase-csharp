using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Resend;

/// <summary>
/// Represents optional parameters used in the Resend API. These parameters provide
/// additional customization for the resend operation, such as including a CAPTCHA
/// token or specifying a redirection URL after email confirmation.
/// </summary>
public class ResendOptions
{
    /// <summary>
    /// Verification token received when the user completes the captcha on the site.
    /// </summary>
    [JsonPropertyName("captchaToken")]
    public string? CaptchaToken { get; set; }

    /// <summary>
    /// A URL or mobile address to send the user to after they are confirmed.
    /// </summary>
    [JsonPropertyName("emailRedirectTo")]
    public string? EmailRedirectTo { get; set; }
}
