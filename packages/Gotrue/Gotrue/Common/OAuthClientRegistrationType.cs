using Supabase.Core.Attributes;

namespace Supabase.Gotrue.Common;

/// <summary>
/// How an OAuth client was registered.
/// </summary>
public enum OAuthClientRegistrationType
{
    /// <summary>The client registered itself through the OAuth dynamic client registration endpoint.</summary>
    [MapTo("dynamic")]
    Dynamic,

    /// <summary>An administrator registered the client.</summary>
    [MapTo("manual")]
    Manual,
}
