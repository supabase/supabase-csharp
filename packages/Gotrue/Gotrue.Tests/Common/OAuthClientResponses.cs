#region

using System;
using System.IO;

#endregion

namespace Gotrue.Tests.Common;

/// <summary>
///     OAuth client bodies as the auth server returns them, read from <c>Common/Data</c>.
/// </summary>
internal static class OAuthClientResponses
{
    internal static string Confidential => Read("oauth_client_confidential.json");

    internal static string Public => Read("oauth_client_public.json");

    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Common", "Data", name));
}
