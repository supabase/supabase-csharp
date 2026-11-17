using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Supabase.ThirdPartyAuth;

internal static class ThirdPartyAuthorization
{
    private const string AuthorizationHeader = "Authorization";

    internal static void RejectConflictingAuthorizationHeader(SupabaseOptions options)
    {
        if (options.ThirdPartyAccessToken is not null && CarriesAuthorization(options.Headers))
            throw new ArgumentException(
                $"{nameof(SupabaseOptions.ThirdPartyAccessToken)} supplies the Authorization header, so {nameof(SupabaseOptions.Headers)} cannot also set one.",
                nameof(options));
    }

    internal static InvalidOperationException AuthDisabled() =>
        new($"Supabase Auth is not available because {nameof(SupabaseOptions)}.{nameof(SupabaseOptions.ThirdPartyAccessToken)} is set: sign-in and sessions belong to the third-party auth provider.");

    internal static Func<Task<string?>>? RealtimeAccessToken(Func<CancellationToken, Task<string?>>? accessToken, string? apiKey) =>
        accessToken is null ? null : () => AccessTokenOrApiKeyAsync(accessToken, apiKey);

    private static async Task<string?> AccessTokenOrApiKeyAsync(Func<CancellationToken, Task<string?>> accessToken, string? apiKey) =>
        await accessToken(CancellationToken.None).ConfigureAwait(false) ?? apiKey;

    private static bool CarriesAuthorization(Dictionary<string, string> headers) =>
        headers.Keys.Any(key => string.Equals(key, AuthorizationHeader, StringComparison.OrdinalIgnoreCase));
}
