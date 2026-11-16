using System.Collections.Generic;
using Supabase.Core.Attributes;
using static Supabase.Gotrue.Constants;

namespace Supabase.Gotrue;

/// <summary>
/// Options used for signing in a user with passwordless Options
/// </summary>
public class SignInWithPasswordlessOptions
{
    /// <summary>
    ///  A custom data object to store the user's metadata. This maps to the `auth.users.user_metadata` column.
    ///  
    /// The `data` should be a JSON serializable object that includes user-specific info, such as their first and last name.
    /// </summary>
    public Dictionary<string, object> Data = new Dictionary<string, object>();

    /// <summary>
    /// Verification token received when the user completes the captcha on the site.
    /// </summary>
    public string? CaptchaToken { get; set; }

    /// <summary>
    /// If set to false, this method will not create a new user. Defaults to true.
    /// </summary>
    public bool ShouldCreateUser { get; set; } = true;
}

/// <inheritdoc />
public class SignInWithPasswordlessEmailOptions : SignInWithPasswordlessOptions
{
    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; private set; }

    /// <summary>
    /// The redirect url embedded in the email link.
    /// </summary>
    public string? EmailRedirectTo { get; set; }

    private OAuthFlowType? chosenFlowType;

    /// <summary>
    /// The OAuth flow type for this sign-in. PKCE is recommended for mobile and server-side applications.
    /// </summary>
    /// <remarks>
    /// When set, it wins over the client's <see cref="ClientOptions.FlowType"/>. When left unset, the client's
    /// <see cref="ClientOptions.FlowType"/> applies to the call, while this property still reads
    /// <see cref="OAuthFlowType.Implicit"/>.
    /// </remarks>
    public OAuthFlowType FlowType
    {
        get => this.chosenFlowType ?? OAuthFlowType.Implicit;
        set => this.chosenFlowType = value;
    }

    /// <param name="email">The user's email address.</param>
    public SignInWithPasswordlessEmailOptions(string email) => this.Email = email;

    internal SignInWithPasswordlessEmailOptions WithDefaultFlowType(OAuthFlowType defaultFlowType)
    {
        var resolved = (SignInWithPasswordlessEmailOptions) this.MemberwiseClone();
        resolved.chosenFlowType = this.chosenFlowType ?? defaultFlowType;
        return resolved;
    }
}

/// <inheritdoc />
public class SignInWithPasswordlessPhoneOptions : SignInWithPasswordlessOptions
{
    /// <summary>
    /// Represents a messaging channel to use for sending the OTP.
    /// </summary>
    public enum MessagingChannel
    {
        /// <summary>
        /// SMS
        /// </summary>
        [MapTo("sms")]
        SMS,
        /// <summary>
        /// 
        /// </summary>
        [MapTo("whatsapp")]
        WHATSAPP
    }

    /// <summary>
    /// The user's phone number
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Messaging channel to use (e.g. whatsapp or sms), Defaults to SMS.
    /// </summary>
    public MessagingChannel Channel { get; set; } = MessagingChannel.SMS;

    /// <param name="phone">The user's phone number</param>
    public SignInWithPasswordlessPhoneOptions(string phone) => this.Phone = phone;
}
