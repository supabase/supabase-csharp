using System;

namespace Supabase.Gotrue;

/// <summary>
/// A utility class that represents a successful response from a request to send a user's password reset using the PKCE flow.
/// </summary>
public class ResetPasswordForEmailOptions
{
    /// <summary>
    /// The Email representing the user's account whose password is being reset.
    /// </summary>
    public string Email { get; private set; }

    /// <summary>
    /// The OAuth Flow Type. 
    /// </summary>
    public Constants.OAuthFlowType FlowType { get; set; } = Constants.OAuthFlowType.Implicit;

    /// <summary>
    /// The URL to send the user to after they click the password reset link.
    /// </summary>
    public string? RedirectTo { get; set; }

    /// <summary>
    /// Verification token received when the user completes the captcha on the site.
    /// </summary>
    public string? CaptchaToken { get; set; }

    /// <summary>
    /// Not read by the SDK. The PKCE verifier is generated when the request is sent and returned on
    /// <see cref="ResetPasswordForEmailState.PKCEVerifier"/>.
    /// </summary>
    [Obsolete("The SDK never reads this property: it generates the PKCE verifier and returns it on ResetPasswordForEmailState. Favor ResetPasswordForEmailState.PKCEVerifier instead. This member will be removed in v9.")]
    public string? PKCEVerifier { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetPasswordForEmailOptions"/> class with the provided email.
    /// </summary>
    /// <param name="email">The email of the user account for which the password is being reset.</param>
    public ResetPasswordForEmailOptions(string email) => this.Email = email;
}