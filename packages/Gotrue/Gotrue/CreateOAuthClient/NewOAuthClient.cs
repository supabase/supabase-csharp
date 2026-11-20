using System;
using System.Collections.Generic;
using System.Linq;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.CreateOAuthClient;

/// <summary>
/// An OAuth client to register, built with <see cref="Confidential" /> or <see cref="Public" />. The factory fixes the
/// client type and the token endpoint authentication method that goes with it, so a client cannot be built with a
/// combination the server rejects.
/// </summary>
public sealed record NewOAuthClient
{
    private NewOAuthClient(OAuthClientType type, string name, IReadOnlyList<Uri> redirectUris, OAuthClientTokenEndpointAuthMethod authMethod)
    {
        this.Type = type;
        this.Name = RequiredName(name);
        this.RedirectUris = RequiredRedirectUris(redirectUris);
        this.TokenEndpointAuthMethod = authMethod;
    }

    /// <summary>Whether the client can keep a secret.</summary>
    public OAuthClientType Type { get; }

    /// <summary>The human-readable name of the client.</summary>
    public string Name { get; }

    /// <summary>The URIs the server may redirect to after an authorization.</summary>
    public IReadOnlyList<Uri> RedirectUris { get; }

    /// <summary>How the client authenticates to the token endpoint.</summary>
    public OAuthClientTokenEndpointAuthMethod TokenEndpointAuthMethod { get; }

    /// <summary>
    /// A client that runs on a server and authenticates with a client secret, which the server generates when the client
    /// is registered.
    /// </summary>
    /// <param name="name">The human-readable name of the client.</param>
    /// <param name="redirectUris">The URIs the server may redirect to after an authorization: at least one, at most ten.</param>
    /// <param name="authMethod">
    /// Whether the client sends its secret in an HTTP Basic header (<see cref="OAuthClientTokenEndpointAuthMethod.ClientSecretBasic" />,
    /// the default) or in the request body (<see cref="OAuthClientTokenEndpointAuthMethod.ClientSecretPost" />).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="name" /> or <paramref name="redirectUris" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> is blank, <paramref name="redirectUris" /> is empty, or <paramref name="authMethod" /> is
    /// <see cref="OAuthClientTokenEndpointAuthMethod.None" />, which a client with a secret cannot use.
    /// </exception>
    public static NewOAuthClient Confidential(string name, IReadOnlyList<Uri> redirectUris, OAuthClientTokenEndpointAuthMethod authMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretBasic) =>
        authMethod == OAuthClientTokenEndpointAuthMethod.None
            ? throw new ArgumentException("A confidential client authenticates with its secret: use ClientSecretBasic or ClientSecretPost.", nameof(authMethod))
            : new NewOAuthClient(OAuthClientType.Confidential, name, redirectUris, authMethod);

    /// <summary>
    /// A client that cannot keep a secret, such as a single-page or mobile app. The server generates no client secret for it.
    /// </summary>
    /// <param name="name">The human-readable name of the client.</param>
    /// <param name="redirectUris">The URIs the server may redirect to after an authorization: at least one, at most ten.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name" /> or <paramref name="redirectUris" /> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name" /> is blank or <paramref name="redirectUris" /> is empty.</exception>
    public static NewOAuthClient Public(string name, IReadOnlyList<Uri> redirectUris) =>
        new(OAuthClientType.Public, name, redirectUris, OAuthClientTokenEndpointAuthMethod.None);

    private static string RequiredName(string name) =>
        name is null ? throw new ArgumentNullException(nameof(name))
        : string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("An OAuth client needs a name.", nameof(name))
        : name;

    private static IReadOnlyList<Uri> RequiredRedirectUris(IReadOnlyList<Uri> redirectUris) =>
        redirectUris is null ? throw new ArgumentNullException(nameof(redirectUris))
        : redirectUris.Count == 0 ? throw new ArgumentException("An OAuth client needs at least one redirect URI.", nameof(redirectUris))
        : redirectUris.ToList();
}
