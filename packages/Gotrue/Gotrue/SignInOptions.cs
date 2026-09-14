using System.Collections.Generic;
using static Supabase.Gotrue.Constants;

namespace Supabase.Gotrue;

/// <summary>
/// Options used for signing in a user.
/// </summary>
public class SignInOptions
{
    /// <summary>
    /// A URL or mobile address to send the user to after they are confirmed.
    /// </summary>
    /// <remarks>
    /// GoTrue preserves the <c>redirect_to</c> query string and echoes it back to your callback, so this is
    /// also the channel for server-side CSRF correlation: the provider-side OAuth <c>state</c> is generated
    /// and validated by the GoTrue server (not the client), so attach your own anti-CSRF token here and
    /// validate it on the callback rather than relying on a <c>state</c> parameter.
    /// </remarks>
    /// <example>
    /// Server-side PKCE flow with CSRF correlation:
    /// <code>
    /// // 1. Initiate: attach an anti-CSRF token to redirect_to and keep the PKCE verifier server-side.
    /// var auth = await client.SignIn(Provider.Google, new SignInOptions
    /// {
    ///     FlowType   = OAuthFlowType.PKCE,
    ///     RedirectTo = $"https://myapp.com/callback?csrf={csrfToken}",
    /// });
    /// session["csrf"] = csrfToken;         // store to validate on the callback
    /// session["pkce"] = auth.PKCEVerifier; // needed to exchange the code
    /// return Redirect(auth.Uri.ToString());
    ///
    /// // 2. Callback (GET /callback?code=...&amp;csrf=...): validate correlation, then exchange.
    /// if (query["csrf"] != session["csrf"]) return Forbid();
    /// var result = await client.ExchangeCodeForSession(session["pkce"], query["code"]);
    /// </code>
    /// </example>
    public string? RedirectTo { get; set; }

    /// <summary>
    /// A space-separated list of scopes granted to the OAuth application.
    /// </summary>
    public string? Scopes { get; set; }

    /// <summary>
    /// An object of key-value pairs containing query parameters granted to the OAuth application.
    /// </summary>
    public Dictionary<string, string>? QueryParams { get; set; }

    /// <summary>
    /// Represents an OAuth Flow type, defaults to `Implicit`
    ///
    /// PKCE is recommended for mobile and server-side applications.
    /// </summary>
    public OAuthFlowType FlowType { get; set; } = OAuthFlowType.Implicit;
}
