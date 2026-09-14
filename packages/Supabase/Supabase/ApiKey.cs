using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Supabase;

/// <summary>
/// Helpers for reasoning about the format of a Supabase API key.
///
/// Supabase is migrating away from the legacy JWT-based <c>anon</c> / <c>service_role</c> keys
/// toward opaque, non-JWT keys: publishable (<c>sb_publishable_…</c>, client-side) and secret
/// (<c>sb_secret_…</c>, server-side). Because the new keys are not JWTs, they must travel on the
/// <c>apikey</c> header only — the platform rejects them on <c>Authorization: Bearer</c> (except
/// when the Bearer value exactly equals the <c>apikey</c> value).
/// </summary>
internal static class ApiKey
{
    private static readonly Regex SubtypePattern = new(@"^sb_[a-zA-Z0-9]+_", RegexOptions.Compiled);

    /// <summary>
    /// Subtypes we've already warned about, so <see cref="CheckFormat"/> stays quiet after the
    /// first occurrence (mirrors supabase-js <c>checkApiKeyFormat</c>).
    /// </summary>
    private static readonly HashSet<string> WarnedSubtypes = new();

    /// <summary>
    /// True when the key is a new-format opaque key (publishable or secret). These are not JWTs
    /// and must never be sent as an <c>Authorization: Bearer</c> token.
    /// </summary>
    public static bool IsNewApiKey(string? key) =>
        key != null && (key.StartsWith("sb_publishable_") || key.StartsWith("sb_secret_"));

    /// <summary>
    /// Warns (once per unrecognized subtype) when the key uses an <c>sb_…_</c> prefix the SDK
    /// does not recognize. Legacy JWT keys (no <c>sb_</c> prefix), the recognized new-format keys,
    /// and temporary <c>sb_temp_</c> keys are all considered fine and produce no warning.
    /// </summary>
    public static void CheckFormat(string? key)
    {
        if (key == null || !key.StartsWith("sb_") || IsNewApiKey(key) || key.StartsWith("sb_temp_"))
            return;

        var subtype = SubtypePattern.Match(key) is { Success: true } match ? match.Value : "unknown";

        lock (WarnedSubtypes)
        {
            if (!WarnedSubtypes.Add(subtype))
                return;
        }

        Trace.TraceWarning(
            "Supabase: Unrecognized API key format '{0}'. If this is a new-format key it may be " +
            "rejected on the Authorization header. Use a publishable (sb_publishable_) key for " +
            "client-side and a secret (sb_secret_) key for server-side contexts.", subtype);
    }
}
