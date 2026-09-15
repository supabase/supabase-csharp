using System.Runtime.Serialization;
using Supabase.Core.Attributes;

namespace Supabase.Gotrue.Resend;

/// <summary>
///     The type of resend.
/// </summary>
public enum ResendType
{
    /// <summary>Signup resend.</summary>
    [MapTo("signup")]
    [EnumMember(Value = "signup")]
    SignUp,

    /// <summary>SMS resend.</summary>
    [MapTo("sms")]
    [EnumMember(Value = "sms")]
    Sms,

    /// <summary>Phone change resend.</summary>
    [MapTo("phone_change")]
    [EnumMember(Value = "phone_change")]
    PhoneChange,

    /// <summary>Email change resend.</summary>
    [MapTo("email_change")]
    [EnumMember(Value = "email_change")]
    EmailChange,
}
