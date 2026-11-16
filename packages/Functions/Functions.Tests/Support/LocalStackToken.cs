using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Functions.Tests.Support;

/// <summary>
///     Issues a JWT the local Supabase CLI stack accepts: signed with the stack's default JWT secret, so the
///     functions gateway lets the invocation through without a real sign-in.
/// </summary>
internal static class LocalStackToken
{
    private const string JwtSecret = "super-secret-jwt-token-with-at-least-32-characters-long";

    internal static string Issue()
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256Signature)
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}
