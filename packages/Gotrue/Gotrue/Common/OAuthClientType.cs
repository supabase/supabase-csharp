using Supabase.Core.Attributes;

namespace Supabase.Gotrue.Common;

/// <summary>
/// Whether an OAuth client can keep a secret.
/// </summary>
public enum OAuthClientType
{
    /// <summary>A client that cannot keep a secret, such as a single-page or mobile app. It has no client secret.</summary>
    [MapTo("public")]
    Public,

    /// <summary>A client that runs on a server and authenticates to the token endpoint with its client secret.</summary>
    [MapTo("confidential")]
    Confidential,
}
