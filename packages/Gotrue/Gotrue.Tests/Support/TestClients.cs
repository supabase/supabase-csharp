#region

using System;
using System.Collections.Generic;
using System.Net.Http;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.StatelessClient;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     One place to build a Gotrue client per test tier: Contract tests get a client pointed at a
///     <see cref="MockGotrueServer" />, E2E tests get one pointed at the local Supabase CLI stack
///     (overridable with SUPABASE_CLI_AUTH_URL / SUPABASE_CLI_ANON_KEY / SUPABASE_CLI_JWT_SECRET).
/// </summary>
internal static class TestClients
{
    internal static readonly string CliAuthUrl =
        Environment.GetEnvironmentVariable("SUPABASE_CLI_AUTH_URL") ?? "http://127.0.0.1:54321/auth/v1";

    internal static readonly string CliAnonKey =
        Environment.GetEnvironmentVariable("SUPABASE_CLI_ANON_KEY") ??
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";

    internal static readonly string CliJwtSecret =
        Environment.GetEnvironmentVariable("SUPABASE_CLI_JWT_SECRET") ??
        "super-secret-jwt-token-with-at-least-32-characters-long";

    /// <summary>
    ///     URL that answers 200 without any header, for network/ping tests: Kong requires the api key, so it
    ///     is passed as a query parameter.
    /// </summary>
    internal static string CliPingUrl => $"{CliAuthUrl}/settings?apikey={CliAnonKey}";

    internal static IGotrueClient<User, Session> AgainstCliStack() => new Client(CliOptions());

    internal static IGotrueAdminClient<User> AdminAgainstCliStack() =>
        AdminAgainstCliStack(GenerateServiceRoleToken(CliJwtSecret));

    internal static IGotrueAdminClient<User> AdminAgainstCliStack(string serviceKey) =>
        new AdminClient(serviceKey, CliOptions());

    internal static IGotrueClient<User, Session> Against(MockGotrueServer server, bool autoRefreshToken = false, HttpClient? httpClient = null) =>
        new Client(new ClientOptions
        {
            Url = server.Url,
            AutoRefreshToken = autoRefreshToken,
            HttpClient = httpClient,
            AllowUnconfirmedUserSessions = false,
            Headers = new Dictionary<string, string> { { "apikey", MockGotrueServer.ApiKey } },
        });

    internal static StatelessClientOptions StatelessAgainstCliStack()
    {
        var options = new StatelessClientOptions { Url = CliAuthUrl, AllowUnconfirmedUserSessions = true };
        options.Headers.Add("apikey", CliAnonKey);
        return options;
    }

    private static ClientOptions CliOptions() =>
        new()
        {
            Url = CliAuthUrl,
            AutoRefreshToken = true,
            AllowUnconfirmedUserSessions = true,
            Headers = new Dictionary<string, string> { { "apikey", CliAnonKey } },
        };
}
