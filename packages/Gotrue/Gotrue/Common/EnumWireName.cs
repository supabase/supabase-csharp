using System;
using System.Collections.Generic;
using System.Linq;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.Common;

internal static class EnumWireName<TEnum> where TEnum : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, TEnum> ValuesByWireName =
        Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToDictionary(value => Core.Helpers.GetMappedToAttr(value).Mapping);

    internal static string Of(TEnum value) => Core.Helpers.GetMappedToAttr(value).Mapping;

    internal static TEnum Parse(string? wireName, string description) =>
        wireName is not null && ValuesByWireName.TryGetValue(wireName, out var value)
            ? value
            : throw new GotrueException($"The auth server returned an unrecognized {description} '{wireName}'.");
}
