using Supabase.Core.Attributes;

namespace Supabase.Gotrue.Common;

/// <summary>
/// How an OAuth client authenticates to the token endpoint.
/// </summary>
public enum OAuthClientTokenEndpointAuthMethod
{
    /// <summary>The client does not authenticate. Only public clients use it.</summary>
    [MapTo("none")]
    None,

    /// <summary>The client sends its id and secret in an HTTP Basic <c>Authorization</c> header.</summary>
    [MapTo("client_secret_basic")]
    ClientSecretBasic,

    /// <summary>The client sends its id and secret in the request body.</summary>
    [MapTo("client_secret_post")]
    ClientSecretPost,
}
