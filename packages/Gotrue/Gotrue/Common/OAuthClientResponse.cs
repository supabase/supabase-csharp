using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.Common;

internal sealed record OAuthClientResponse
{
    [JsonPropertyName("client_id")]
    public Guid ClientId { get; init; }

    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("client_type")]
    public string? ClientType { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; init; }

    [JsonPropertyName("registration_type")]
    public string? RegistrationType { get; init; }

    [JsonPropertyName("redirect_uris")]
    public IReadOnlyList<Uri>? RedirectUris { get; init; }

    [JsonPropertyName("grant_types")]
    public IReadOnlyList<string>? GrantTypes { get; init; }

    [JsonPropertyName("client_uri")]
    public Uri? ClientUri { get; init; }

    [JsonPropertyName("logo_uri")]
    public Uri? LogoUri { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    internal static OAuthClientCredentials ReadCredentials(OAuthClientResponse? response) =>
        Require(response).ToCredentials();

    private static OAuthClientResponse Require(OAuthClientResponse? response) =>
        response ?? throw new GotrueException("The auth server returned no OAuth client.");

    private OAuthClientCredentials ToCredentials() =>
        new() { Client = this.ToOAuthClient(), ClientSecret = this.ClientSecret };

    private OAuthClient ToOAuthClient() =>
        new()
        {
            ClientId = this.ClientId,
            Name = this.ClientName,
            Type = EnumWireName<OAuthClientType>.Parse(this.ClientType, "OAuth client type"),
            TokenEndpointAuthMethod = EnumWireName<OAuthClientTokenEndpointAuthMethod>.Parse(this.TokenEndpointAuthMethod, "OAuth client token endpoint authentication method"),
            RegistrationType = EnumWireName<OAuthClientRegistrationType>.Parse(this.RegistrationType, "OAuth client registration type"),
            RedirectUris = this.RedirectUris ?? Array.Empty<Uri>(),
            GrantTypes = (this.GrantTypes ?? Array.Empty<string>()).Select(grantType => EnumWireName<OAuthClientGrantType>.Parse(grantType, "OAuth grant type")).ToList(),
            ClientUri = this.ClientUri,
            LogoUri = this.LogoUri,
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt,
        };
}
