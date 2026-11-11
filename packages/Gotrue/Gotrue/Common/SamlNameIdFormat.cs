using Supabase.Core.Attributes;

namespace Supabase.Gotrue.Common;

/// <summary>
/// The format of the SAML <c>NameID</c> an identity provider asserts to identify the user.
/// </summary>
public enum SamlNameIdFormat
{
    /// <summary>A stable, opaque identifier that stays the same across sign-ins.</summary>
    [MapTo("urn:oasis:names:tc:SAML:2.0:nameid-format:persistent")]
    Persistent,

    /// <summary>The user's email address.</summary>
    [MapTo("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress")]
    EmailAddress,

    /// <summary>A one-time identifier that changes on every sign-in.</summary>
    [MapTo("urn:oasis:names:tc:SAML:2.0:nameid-format:transient")]
    Transient,

    /// <summary>Leaves the format up to the identity provider.</summary>
    [MapTo("urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified")]
    Unspecified,
}
