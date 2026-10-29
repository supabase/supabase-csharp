#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Mfa;

#endregion

#pragma warning disable CS1591

namespace Supabase.Gotrue;

/// <summary>
///     Represents a Gotrue User
///     Ref: https://supabase.github.io/gotrue-js/interfaces/User.html
/// </summary>
public class User
{
    [JsonPropertyName("app_metadata")]
    public Dictionary<string, object> AppMetadata { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("aud")]
    public string? Aud { get; set; }

    [JsonPropertyName("confirmation_sent_at")]
    public DateTime? ConfirmationSentAt { get; set; }

    [JsonPropertyName("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_confirmed_at")]
    public DateTime? EmailConfirmedAt { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("identities")]
    public List<UserIdentity> Identities { get; set; } = new List<UserIdentity>();

    [JsonPropertyName("invited_at")]
    public DateTime? InvitedAt { get; set; }

    [JsonPropertyName("last_sign_in_at")]
    public DateTime? LastSignInAt { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("phone_confirmed_at")]
    public DateTime? PhoneConfirmedAt { get; set; }

    [JsonPropertyName("recovery_sent_at")]
    public DateTime? RecoverySentAt { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("banned_until")]
    public DateTime? BannedUntil { get; set; }

    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; set; }

    [JsonPropertyName("factors")]
    public List<Factor> Factors { get; set; } = new List<Factor>();

    [JsonPropertyName("user_metadata")]
    public Dictionary<string, object> UserMetadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    ///     Whether the user has been confirmed by any means — an explicit confirmation, or email or phone
    ///     verification. Auto-confirmed sign-ups (with <c>enable_confirmations = false</c>) populate only
    ///     <see cref="EmailConfirmedAt" /> / <see cref="PhoneConfirmedAt" />, not <see cref="ConfirmedAt" />.
    /// </summary>
    [JsonIgnore]
    public bool IsConfirmed =>
        this.ConfirmedAt != null || this.EmailConfirmedAt != null || this.PhoneConfirmedAt != null;
}

/// <summary>
///     Ref: https://supabase.github.io/gotrue-js/interfaces/AdminUserAttributes.html
/// </summary>
public class AdminUserAttributes : UserAttributes
{
    /// <summary>
    ///     A custom data object for app_metadata that. Can be any JSON serializable data.
    ///     Only a service role can modify
    ///     Note: GoTrue does not yest support creating a user with app metadata
    ///     (see:
    ///     https://github.com/supabase/gotrue-js/blob/d7b334a4283027c65814aa81715ffead262f0bfa/test/GoTrueApi.test.ts#L45)
    /// </summary>
    [JsonPropertyName("app_metadata")]
    public Dictionary<string, object> AppMetadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    ///     A custom data object for user_metadata. Can be any JSON serializable data.
    ///     Only a service role can modify.
    /// </summary>
    [JsonPropertyName("user_metadata")]
    public Dictionary<string, object> UserMetadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    ///     Sets if a user has confirmed their email address.
    ///     Only a service role can modify
    /// </summary>
    [JsonPropertyName("email_confirm")]
    public bool? EmailConfirm { get; set; }

    /// <summary>
    ///     Sets if a user has confirmed their phone number.
    ///     Only a service role can modify
    /// </summary>
    [JsonPropertyName("phone_confirm")]
    public bool? PhoneConfirm { get; set; }

    /// <summary>
    ///     <para>Determines how long a user is banned for. </para>
    ///     <para>
    ///         This property is ignored when creating a user.
    ///         If you want to create a user banned, first create the user then update it sending this property.
    ///     </para>
    ///     <para>
    ///         The format for the ban duration follows a strict sequence of decimal numbers with a unit suffix.
    ///         Valid time units are "ns", "us" (or "µs"), "ms", "s", "m", "h".
    ///     </para>
    ///     <para>For example, some possible durations include: '300ms', '2h45m', '1200s'.</para>
    ///     <para>Setting the ban duration to "none" lifts the ban on the user.</para>
    ///     <para>Only a service role can modify.</para>
    /// </summary>
    [JsonPropertyName("ban_duration")]
    public string? BanDuration { get; set; }
}

/// <summary>
///     Ref: https://supabase.github.io/gotrue-js/interfaces/UserAttributes.html
/// </summary>
public class UserAttributes
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_change_token")]
    public string? EmailChangeToken { get; set; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    /// <summary>
    ///     A custom data object for user_metadata that a user can modify.Can be any JSON.
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
}

/// <summary>
///     Ref: https://supabase.github.io/gotrue-js/interfaces/VerifyEmailOTPParams.html
/// </summary>
public class VerifyOTPParams
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class UserList<TUser>
    where TUser : User
{
    [JsonPropertyName("aud")]
    public string? Aud { get; set; }

    [JsonPropertyName("users")]
    public List<TUser> Users { get; set; } = new List<TUser>();
}

/// <summary>
///     Ref: https://supabase.github.io/gotrue-js/interfaces/UserIdentity.html
/// </summary>
public class UserIdentity
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("identity_data")]
    public Dictionary<string, object> IdentityData { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("identity_id")]
    public string IdentityId { get; set; } = null!;

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("last_sign_in_at")]
    public DateTime LastSignInAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
