#region

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using Microsoft.IdentityModel.Tokens;

#endregion

namespace Gotrue.Tests;

/// <summary>
///     Shared, tier-agnostic test data helpers: unique identities per test (so E2E runs stay isolated on the
///     live stack), a strong default password, and a signed service-role token for admin calls.
/// </summary>
public static class TestUtils
{
    public const string Password = "I@M@SuperP@ssWord";
    private static readonly Random Random = new();

    public static string RandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length).Select(s => s[Random.Next(s.Length)]).ToArray());
    }

    public static string RandomEmail() => $"{RandomString(12)}@supabase.io";

    public static string GetRandomPhoneNumber()
    {
        const string chars = "123456789";
        var inner = new string(Enumerable.Repeat(chars, 10).Select(s => s[Random.Next(s.Length)]).ToArray());
        return $"+1{inner}";
    }

    /// <summary>
    ///     Returns a random number within the limits specified via parameters.
    /// </summary>
    public static int RandomNumber(int minValue = 0, int maxValue = 1000) => Random.Next(minValue, maxValue);

    public static string GenerateServiceRoleToken(string jwtSecret)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        // Backdated so sub-second clock skew against the containerized server cannot fail gotrue's zero-leeway nbf check.
        var issuedAt = DateTime.UtcNow.AddMinutes(-1);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256Signature),
            Claims = new Dictionary<string, object> { { "role", "service_role" } },
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(securityToken);
    }
}
