using System;

namespace Supabase.Storage.Common;

internal static class PathSegment
{
    private static readonly string[] RelativeSegments = { ".", ".." };

    internal static string Escape(string value, string subject, string paramName)
    {
        RejectBlank(value, subject, paramName);
        RejectRelative(value, subject, paramName);
        return Uri.EscapeDataString(value);
    }

    internal static void RejectBlank(string value, string subject, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{subject} must be provided.", paramName);
    }

    private static void RejectRelative(string value, string subject, string paramName)
    {
        if (Array.IndexOf(RelativeSegments, value) >= 0)
            throw new ArgumentException($"{subject} cannot be a relative path segment.", paramName);
    }
}
