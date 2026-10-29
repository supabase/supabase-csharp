#region

using System;
using System.IO;

#endregion

namespace Gotrue.Tests.Common;

/// <summary>
///     Custom provider bodies as the auth server returns them, read from <c>Common/Data</c>.
/// </summary>
internal static class CustomProviderResponses
{
    internal static string OAuth2 => Read("custom_provider_oauth2.json");

    internal static string Oidc => Read("custom_provider_oidc.json");

    internal static string SparseOidc => Read("custom_provider_oidc_sparse.json");

    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Common", "Data", name));
}
