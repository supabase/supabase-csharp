using System;
using System.IO;

namespace Gotrue.Tests.AuthorizationOAuth;

/// <summary>
///     OAuthAuthorization bodies as the auth server returns them
/// </summary>
internal static class AuthorizationOAuthResponses
{
    internal static string Redirect => Read("authorization_redirect.json");

    internal static string Details => Read("authorization_details.json");

    internal static string Grants => Read("oauth_grants.json");

    private static string Read(string name) =>
        File.ReadAllText(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AuthorizationOAuth", "Data", name)
        );
}
