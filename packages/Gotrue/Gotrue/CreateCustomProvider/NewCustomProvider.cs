using System;
using System.Text;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CustomProviders;

namespace Supabase.Gotrue.CreateCustomProvider;

/// <summary>
/// A custom provider to register, built with <see cref="OAuth2" /> or <see cref="Oidc" /> so each protocol carries
/// the settings it needs. Optional settings go in <see cref="CustomProviderOptions" />.
/// </summary>
public sealed record NewCustomProvider
{
    private NewCustomProvider(CustomProviderType type, string identifier, string name, string clientId, string clientSecret)
    {
        this.Type = type;
        this.Identifier = CustomProviderIdentifier.EnsureRoutable(identifier);
        this.Name = Required(name, nameof(name));
        this.ClientId = Required(clientId, nameof(clientId));
        this.ClientSecret = Required(clientSecret, nameof(clientSecret));
    }

    /// <summary>The protocol the provider speaks.</summary>
    public CustomProviderType Type { get; }

    /// <summary>The identifier users will sign in with, starting with <c>custom:</c>.</summary>
    public string Identifier { get; }

    /// <summary>The display name.</summary>
    public string Name { get; }

    /// <summary>The OAuth client id registered with the provider.</summary>
    public string ClientId { get; }

    /// <summary>The OAuth client secret. The server encrypts it and never returns it.</summary>
    public string ClientSecret { get; }

    /// <summary>The OIDC issuer. Null for OAuth 2.0 providers.</summary>
    public Uri? Issuer { get; private init; }

    /// <summary>The OAuth 2.0 authorization endpoint. Null for OIDC providers.</summary>
    public Uri? AuthorizationUrl { get; private init; }

    /// <summary>The OAuth 2.0 token endpoint. Null for OIDC providers.</summary>
    public Uri? TokenUrl { get; private init; }

    /// <summary>The OAuth 2.0 userinfo endpoint. Null for OIDC providers.</summary>
    public Uri? UserInfoUrl { get; private init; }

    /// <summary>
    /// A plain OAuth 2.0 provider. Every URL must use HTTPS and resolve to a public host.
    /// </summary>
    /// <param name="identifier">The identifier users will sign in with, starting with <c>custom:</c> (for example <c>custom:mycompany</c>).</param>
    /// <param name="name">The display name.</param>
    /// <param name="clientId">The OAuth client id registered with the provider.</param>
    /// <param name="clientSecret">The OAuth client secret.</param>
    /// <param name="authorizationUrl">The provider's authorization endpoint.</param>
    /// <param name="tokenUrl">The provider's token endpoint.</param>
    /// <param name="userInfoUrl">The provider's userinfo endpoint.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The identifier lacks the <c>custom:</c> prefix or contains <c>/</c>, <c>\</c>, <c>?</c>, <c>#</c> or <c>%</c>.
    /// </exception>
    public static NewCustomProvider OAuth2(string identifier, string name, string clientId, string clientSecret, Uri authorizationUrl, Uri tokenUrl, Uri userInfoUrl) =>
        new(CustomProviderType.OAuth2, identifier, name, clientId, clientSecret)
        {
            AuthorizationUrl = Required(authorizationUrl, nameof(authorizationUrl)),
            TokenUrl = Required(tokenUrl, nameof(tokenUrl)),
            UserInfoUrl = Required(userInfoUrl, nameof(userInfoUrl)),
        };

    /// <summary>
    /// An OpenID Connect provider. The server fetches and validates the issuer's discovery document when the
    /// provider is created, so the issuer must be reachable over HTTPS.
    /// </summary>
    /// <param name="identifier">The identifier users will sign in with, starting with <c>custom:</c> (for example <c>custom:mycompany</c>).</param>
    /// <param name="name">The display name.</param>
    /// <param name="clientId">The OAuth client id registered with the provider.</param>
    /// <param name="clientSecret">The OAuth client secret.</param>
    /// <param name="issuer">The OIDC issuer.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The identifier lacks the <c>custom:</c> prefix or contains <c>/</c>, <c>\</c>, <c>?</c>, <c>#</c> or <c>%</c>.
    /// </exception>
    public static NewCustomProvider Oidc(string identifier, string name, string clientId, string clientSecret, Uri issuer) =>
        new(CustomProviderType.Oidc, identifier, name, clientId, clientSecret)
        {
            Issuer = Required(issuer, nameof(issuer)),
        };

    private static T Required<T>(T value, string parameterName) where T : class =>
        value ?? throw new ArgumentNullException(parameterName);

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Type = {this.Type}, Identifier = {this.Identifier}, Name = {this.Name}, ClientId = {this.ClientId}");
        return true;
    }
}
