using System;
using System.Text;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.UpdateCustomProvider;

/// <summary>
/// Changes to a custom provider. A member left null keeps its current value; the type and identifier cannot change.
/// Settings of the other protocol, such as <see cref="Issuer" /> on an OAuth 2.0 provider, are ignored by the server.
/// </summary>
public sealed record CustomProviderChanges
{
    /// <summary>The new display name.</summary>
    public string? Name { get; init; }

    /// <summary>The new OAuth client id.</summary>
    public string? ClientId { get; init; }

    /// <summary>The new OAuth client secret. The server encrypts it and never returns it.</summary>
    public string? ClientSecret { get; init; }

    /// <summary>OIDC only: the new issuer. The server fetches and validates its discovery document again.</summary>
    public Uri? Issuer { get; init; }

    /// <summary>OAuth 2.0 only: the new authorization endpoint.</summary>
    public Uri? AuthorizationUrl { get; init; }

    /// <summary>OAuth 2.0 only: the new token endpoint.</summary>
    public Uri? TokenUrl { get; init; }

    /// <summary>OAuth 2.0 only: the new userinfo endpoint.</summary>
    public Uri? UserInfoUrl { get; init; }

    /// <summary>Changes to the optional settings.</summary>
    public CustomProviderOptions? Options { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Name = {this.Name}, ClientId = {this.ClientId}, Issuer = {this.Issuer}, AuthorizationUrl = {this.AuthorizationUrl}, TokenUrl = {this.TokenUrl}, UserInfoUrl = {this.UserInfoUrl}, Options = {this.Options}");
        return true;
    }
}
