using System.Collections.Generic;
using Supabase.Core.Attributes;

#pragma warning disable CS1591

namespace Supabase.Gotrue;

/// <summary>
/// Constants used throughout the Gotrue Client
/// </summary>
public static class Constants
{
    /// <summary>
    /// URL for the Gotrue server. Defaults to localhost:9999.
    /// </summary>
    public const string GOTRUE_URL = "http://localhost:9999";
    public const string AUDIENCE = "";
    public const int EXPIRY_MARGIN = 60 * 1000;
    public const string STORAGE_KEY = "supabase.auth.token";
    public static readonly Dictionary<string, object> CookieOptions = new Dictionary<string, object>
    {
        { "name", "sb:token" },
        { "lifetime", 60 * 60 * 8 },
        { "domain", "" },
        { "path", "/" },
        { "sameSite", "lax" },
    };

    public enum SortOrder
    {
        [MapTo("asc")]
        Ascending,

        [MapTo("desc")]
        Descending,
    }

    public enum MobileOtpType
    {
        [MapTo("sms")]
        SMS,

        [MapTo("phone_change")]
        PhoneChange,
    }

    public enum EmailOtpType
    {
        [MapTo("signup")]
        Signup,

        [MapTo("invite")]
        Invite,

        [MapTo("magiclink")]
        MagicLink,

        [MapTo("recovery")]
        Recovery,

        [MapTo("email_change")]
        EmailChange,

        [MapTo("email")]
        Email,
    }

    /// <summary>
    /// Providers available to Supabase
    /// Ref: https://supabase.github.io/gotrue-js/modules.html#Provider
    /// </summary>
    public enum Provider
    {
        [MapTo("anonymous_users")]
        AnonymousUsers,

        [MapTo("apple")]
        Apple,

        [MapTo("azure")]
        Azure,

        [MapTo("bitbucket")]
        Bitbucket,

        [MapTo("discord")]
        Discord,

        [MapTo("facebook")]
        Facebook,

        [MapTo("figma")]
        Figma,

        [MapTo("fly")]
        Fly,

        [MapTo("github")]
        Github,

        [MapTo("gitlab")]
        Gitlab,

        [MapTo("google")]
        Google,

        [MapTo("kakao")]
        Kakao,

        [MapTo("keycloak")]
        KeyCloak,

        [MapTo("linkedin")]
        LinkedIn,

        [MapTo("linkedin_oidc")]
        LinkedInOIDC,

        [MapTo("notion")]
        Notion,

        [MapTo("slack")]
        Slack,

        [MapTo("spotify")]
        Spotify,

        [MapTo("twitch")]
        Twitch,

        [MapTo("twitter")]
        Twitter,

        [MapTo("workos")]
        WorkOS,

        [MapTo("zoom")]
        Zoom,
    }

    /// <summary>
    /// States that the Auth Client will raise events for.
    /// </summary>
    public enum AuthState
    {
        SignedIn,
        SignedOut,
        UserUpdated,
        PasswordRecovery,
        TokenRefreshed,
        Shutdown,
        MfaChallengeVerified,
    }

    /// <summary>
    /// Specifies the functionality expected from the `SignIn` method
    /// </summary>
    public enum SignInType
    {
        Email,
        Phone,
        RefreshToken,
    }

    /// <summary>
    /// Represents an OAuth Flow type
    /// </summary>
    public enum OAuthFlowType
    {
        [MapTo("implicit")]
        Implicit,

        [MapTo("pkce")]
        PKCE,
    }

    /// <summary>
    /// Specifies the functionality expected from the `SignUp` method
    /// </summary>
    public enum SignUpType
    {
        Email,
        Phone,
    }

    public enum SignOutScope
    {
        [MapTo("global")]
        Global,

        [MapTo("local")]
        Local,

        [MapTo("others")]
        Others,
    }
}
