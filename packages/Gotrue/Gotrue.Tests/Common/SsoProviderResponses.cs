#region

using System;
using System.IO;

#endregion

namespace Gotrue.Tests.Common;

/// <summary>
///     SSO provider bodies as the auth server returns them, read from <c>Common/Data</c>.
/// </summary>
internal static class SsoProviderResponses
{
    internal static string Complete => Read("sso_provider.json");

    internal static string Sparse => Read("sso_provider_sparse.json");

    internal static string WithoutSaml => Read("sso_provider_without_saml.json");

    internal static string WithNameIdFormat(string urn) => Read("sso_provider_with_name_id_format.json").Replace("{{name_id_format}}", urn);

    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Common", "Data", name));
}
