using System;
using System.Runtime.CompilerServices;

namespace Supabase.Gotrue.UpdateFactor;

internal static class CanonicalUuid
{
    private const string HyphenatedFormat = "D";

    internal static string EnsureRoutable(string value, [CallerArgumentExpression(nameof(value))] string? argumentName = null)
    {
        if (value is null)
            throw new ArgumentNullException(argumentName);
        if (!Guid.TryParseExact(value, HyphenatedFormat, out _))
            throw new ArgumentException("The id must be a UUID in its canonical form, for example '2b306a77-21dc-4110-ba71-537cb56b9e98'.", argumentName);
        return value;
    }
}
