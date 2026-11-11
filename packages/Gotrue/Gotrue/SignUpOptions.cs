using System.Collections.Generic;
namespace Supabase.Gotrue;

/// <summary>
/// Options used for signing up a user.
/// </summary>
public class SignUpOptions : SignInOptions
{
    /// <summary>
    /// Optional user metadata.
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// Verification token received when the user completes the captcha on the site.
    /// </summary>
    public string? CaptchaToken { get; set; }

    /// <summary>
    /// The channel a phone sign-up sends its confirmation code through. Defaults to SMS. Only
    /// <see cref="Interfaces.IGotrueClient{TUser,TSession}.SignUpAsync" /> sends it.
    /// </summary>
    public SignInWithPasswordlessPhoneOptions.MessagingChannel Channel { get; set; } = SignInWithPasswordlessPhoneOptions.MessagingChannel.SMS;
}
