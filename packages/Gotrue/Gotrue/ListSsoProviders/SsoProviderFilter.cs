using System;
using System.Linq;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.ListSsoProviders;

/// <summary>
/// Which single sign-on identity providers a listing returns: the one registered with a resource ID, with
/// <see cref="ByResourceId" />, or those whose resource ID starts with a prefix, with <see cref="ByResourceIdPrefix" />.
/// </summary>
public sealed record SsoProviderFilter
{
    private const string ResourceIdParameter = "resource_id";
    private const string ResourceIdPrefixParameter = "resource_id_prefix";
    private const string LikeEscape = "\\";

    private static readonly string[] LikePatternCharacters = { LikeEscape, "%", "_" };

    private SsoProviderFilter(string parameter, string value)
    {
        this.Parameter = parameter;
        this.Value = value;
    }

    internal string Parameter { get; }

    internal string Value { get; }

    /// <summary>Only the provider registered with this resource ID.</summary>
    /// <param name="resourceId">The provider's <see cref="SsoProvider.ResourceId" />, matched exactly.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resourceId" /> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="resourceId" /> is empty.</exception>
    public static SsoProviderFilter ByResourceId(string resourceId) =>
        new(ResourceIdParameter, EnsureNotEmpty(resourceId, nameof(resourceId), "A resource ID cannot be empty."));

    /// <summary>Only the providers whose resource ID starts with this prefix.</summary>
    /// <param name="prefix">
    /// The start of the providers' <see cref="SsoProvider.ResourceId" />, matched literally: <c>%</c>, <c>_</c> and
    /// <c>\</c> stand for themselves.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="prefix" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="prefix" /> is empty. List without a filter to get every provider.
    /// </exception>
    public static SsoProviderFilter ByResourceIdPrefix(string prefix) =>
        new(ResourceIdPrefixParameter, EscapeLikePattern(EnsureNotEmpty(prefix, nameof(prefix), "A resource ID prefix cannot be empty.")));

    private static string EnsureNotEmpty(string value, string parameterName, string emptyMessage)
    {
        if (value is null)
            throw new ArgumentNullException(parameterName);
        if (value.Length == 0)
            throw new ArgumentException(emptyMessage, parameterName);
        return value;
    }

    private static string EscapeLikePattern(string prefix) =>
        LikePatternCharacters.Aggregate(prefix, (escaped, character) => escaped.Replace(character, LikeEscape + character));
}
