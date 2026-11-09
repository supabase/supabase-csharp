namespace Supabase.Gotrue.Resend;

/// <summary>
/// The outcome of a resend request.
/// </summary>
public class ResendState
{
    /// <summary>
    /// PKCE Verifier generated if using the PKCE flow type.
    /// </summary>
    public string? PKCEVerifier { get; set; }
}
