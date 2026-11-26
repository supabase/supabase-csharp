using System;
using System.Collections.Generic;
using System.Linq;

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

    internal static string EscapeObjectPath(string path, string paramName)
    {
        var segments = path.Split('/');
        RejectLeavingBucket(segments, paramName);
        return string.Join("/", segments.Select(Uri.EscapeDataString));
    }

    private static void RejectLeavingBucket(string[] segments, string paramName)
    {
        if (IsRelative(segments[0]) || ClimbsAboveStart(segments.Skip(1)))
            throw new ArgumentException("An object path cannot leave its bucket.", paramName);
    }

    private static bool ClimbsAboveStart(IEnumerable<string> segments)
    {
        var depth = 0;
        return segments.Any(segment => (depth += DepthChange(segment)) < 0);
    }

    private static int DepthChange(string segment) =>
        segment switch
        {
            "." => 0,
            ".." => -1,
            _ => 1,
        };

    private static bool IsRelative(string segment) => Array.IndexOf(RelativeSegments, segment) >= 0;

    internal static void RejectBlank(string value, string subject, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{subject} must be provided.", paramName);
    }

    private static void RejectRelative(string value, string subject, string paramName)
    {
        if (IsRelative(value))
            throw new ArgumentException($"{subject} cannot be a relative path segment.", paramName);
    }
}
