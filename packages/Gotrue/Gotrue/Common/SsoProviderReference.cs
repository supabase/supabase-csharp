using System;
using System.Linq;
using System.Text;

namespace Supabase.Gotrue.Common;

/// <summary>
/// Which single sign-on identity provider a call addresses: by its <see cref="SsoProvider.Id" /> with
/// <see cref="ById" />, or by the <see cref="SsoProvider.ResourceId" /> it was registered with, with
/// <see cref="ByResourceId" />.
/// </summary>
public sealed record SsoProviderReference
{
    private const string ResourceIdPrefix = "resource_";

    private static readonly string[] CharactersTheServerReadsOnlyUnescaped = { "+", "$", "&", ",", ":", ";", "=", "@" };

    private readonly string addressee;

    private SsoProviderReference(string routeSegment, string addressee)
    {
        this.RouteSegment = routeSegment;
        this.addressee = addressee;
    }

    internal string RouteSegment { get; }

    /// <summary>The provider with this id.</summary>
    /// <param name="id">The provider's <see cref="SsoProvider.Id" />.</param>
    public static SsoProviderReference ById(Guid id) => new(id.ToString(), $"Id = {id}");

    /// <summary>The provider registered with this resource ID.</summary>
    /// <param name="resourceId">The provider's <see cref="SsoProvider.ResourceId" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resourceId" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="resourceId" /> is empty, or contains <c>/</c>: the auth server cannot address such a provider
    /// by its resource ID. Address it <see cref="ById" /> instead.
    /// </exception>
    public static SsoProviderReference ByResourceId(string resourceId) =>
        new(ResourceIdPrefix + EscapeForRoute(EnsureAddressable(resourceId)), $"ResourceId = {resourceId}");

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(this.addressee);
        return true;
    }

    private static string EnsureAddressable(string resourceId)
    {
        if (resourceId is null)
            throw new ArgumentNullException(nameof(resourceId));
        if (resourceId.Length == 0)
            throw new ArgumentException("A resource ID cannot be empty.", nameof(resourceId));
        if (resourceId.Contains('/'))
            throw new ArgumentException("The auth server cannot address a provider by a resource ID containing '/': address it by id instead.", nameof(resourceId));
        return resourceId;
    }

    private static string EscapeForRoute(string resourceId) =>
        CharactersTheServerReadsOnlyUnescaped.Aggregate(Uri.EscapeDataString(resourceId),
            (escaped, character) => escaped.Replace(Uri.EscapeDataString(character), character));
}
