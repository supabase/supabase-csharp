using System;
using System.Runtime.CompilerServices;

namespace Supabase.Gotrue;

internal static class PathSegment
{
    internal static string Escape(string value, [CallerArgumentExpression(nameof(value))] string? argumentName = null)
    {
        if (value is null)
            throw new ArgumentNullException(argumentName);
        if (value is "." or "..")
            throw new ArgumentException("A resource id cannot be '.' or '..': System.Uri resolves it into another route.", argumentName);
        return Uri.EscapeDataString(value);
    }
}
