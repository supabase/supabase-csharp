using System;
namespace Supabase.Gotrue;

/// <summary>
/// Represents an OAuth Provider's URI and Parameters.
///
/// For use with Provider Auth, PKCE Auth, and ID Token auth.
/// </summary>
public class ProviderAuthState
{
    /// <summary>
    /// The Generated Provider's URI
    /// </summary>
    public Uri Uri { get; set; }

    /// <summary>
    /// The PKCE Verifier nonce, only set during a PKCE auth flow.
    /// </summary>
    public string? PKCEVerifier { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="uri"></param>
    public ProviderAuthState(Uri uri) => this.Uri = uri;
}
