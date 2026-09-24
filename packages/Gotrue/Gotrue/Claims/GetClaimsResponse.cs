using System;

namespace Supabase.Gotrue.Claims;

/// <summary>
/// The header and claims of a verified JWT.
/// </summary>
public sealed record GetClaimsResponse
{
    /// <summary>The claims carried by the token payload.</summary>
    public JwtClaims Claims { get; init; } = new();

    /// <summary>The token header.</summary>
    public JwtHeader Header { get; init; } = new();

    /// <summary>The raw signature bytes of the token.</summary>
    public byte[] Signature { get; init; } = Array.Empty<byte>();
}
