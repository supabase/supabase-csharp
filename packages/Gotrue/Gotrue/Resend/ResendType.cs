using Supabase.Core.Attributes;

namespace Supabase.Gotrue.Resend;

/// <summary>
///     The type of resend.
/// </summary>
public enum ResendType
{
    /// <summary>Signup resend.</summary>
    [MapTo("signup")]
    SignUp,

    /// <summary>SMS resend.</summary>
    [MapTo("sms")]
    Sms,

    /// <summary>Phone change resend.</summary>
    [MapTo("phone_change")]
    PhoneChange,

    /// <summary>Email change resend.</summary>
    [MapTo("email_change")]
    EmailChange,
}
