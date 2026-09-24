using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Supabase.Gotrue.Exceptions;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

namespace Supabase.Gotrue.Claims;

/// <summary>
/// Decoding, expiry and signature checks behind GetClaimsAsync.
/// </summary>
internal static class JwtVerification
{
    private static readonly JwtSecurityTokenHandler Handler = new JwtSecurityTokenHandler();

    internal static JwtSecurityToken Decode(string token)
    {
        try
        {
            return Handler.ReadJwtToken(token);
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenException)
        {
            throw new GotrueException("The token could not be read as a JWT.", InvalidJwt, ex);
        }
    }

    internal static void ValidateExpiry(JwtSecurityToken token)
    {
        if (!token.Payload.ContainsKey("exp"))
        {
            throw new GotrueException("The token is missing the exp claim.", InvalidJwt);
        }
        if (token.ValidTo <= DateTime.UtcNow)
        {
            throw new GotrueException("The token has expired.", InvalidJwt);
        }
    }

    internal static void VerifySignature(string token, Jwk jwk)
    {
        var parameters = new TokenValidationParameters
        {
            IssuerSigningKey = new JsonWebKey
            {
                Kty = jwk.Kty,
                Kid = jwk.Kid,
                Alg = jwk.Alg,
                Use = jwk.Use,
                N = jwk.N,
                E = jwk.E,
                Crv = jwk.Crv,
                X = jwk.X,
                Y = jwk.Y,
            },
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
        };
        try
        {
            Handler.ValidateToken(token, parameters, out _);
        }
        // IdentityModel throws NullReferenceException on an array claim with a null entry.
        catch (Exception ex) when (ex is SecurityTokenException or NullReferenceException)
        {
            throw new GotrueException("The token could not be verified.", InvalidJwt, ex);
        }
    }

    internal static GetClaimsResponse BuildResponse(JwtSecurityToken token) =>
        new GetClaimsResponse
        {
            Claims = Deserialize<JwtClaims>(token.RawPayload),
            Header = Deserialize<JwtHeader>(token.RawHeader),
            Signature = Base64UrlEncoder.DecodeBytes(token.RawSignature),
        };

    // Claim names are case sensitive (RFC 7519 §7.3).
    private static T Deserialize<T>(string segment)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(Base64UrlEncoder.DecodeBytes(segment))!;
        }
        catch (JsonException ex)
        {
            throw new GotrueException("The token's claims could not be read.", InvalidJwt, ex);
        }
    }
}
