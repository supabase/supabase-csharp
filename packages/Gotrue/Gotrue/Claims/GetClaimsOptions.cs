namespace Supabase.Gotrue.Claims;

/// <summary>
/// Options for <see cref="Client.GetClaimsAsync" />.
/// </summary>
public sealed record GetClaimsOptions
{
    /// <summary>Skips the local check of the "exp" claim. The signature is still verified.</summary>
    public bool AllowExpired { get; init; }

    /// <summary>Keys tried before the cached set and the server.</summary>
    public Jwks? Jwks { get; init; }
}
