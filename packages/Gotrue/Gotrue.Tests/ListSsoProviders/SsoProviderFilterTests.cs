#region

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.ListSsoProviders;

#endregion

namespace Gotrue.Tests.ListSsoProviders;

/// <summary>
///     The resource IDs and prefixes a provider filter refuses before any request: those that would filter nothing.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SsoProviderFilterTests
{
    [TestMethod]
    public void ByResourceId_ShouldThrowArgumentNull_GivenNullResourceId()
    {
        var filter = () => SsoProviderFilter.ByResourceId(null!);
        filter.Should().ThrowExactly<ArgumentNullException>().WithParameterName("resourceId");
    }

    [TestMethod]
    public void ByResourceId_ShouldThrowArgument_GivenEmptyResourceId()
    {
        var filter = () => SsoProviderFilter.ByResourceId(string.Empty);
        filter.Should().ThrowExactly<ArgumentException>().WithParameterName("resourceId").WithMessage("A resource ID cannot be empty.*",
            "the auth server ignores an empty resource_id and lists every provider, which is not what filtering by one asks for");
    }

    [TestMethod]
    public void ByResourceIdPrefix_ShouldThrowArgumentNull_GivenNullPrefix()
    {
        var filter = () => SsoProviderFilter.ByResourceIdPrefix(null!);
        filter.Should().ThrowExactly<ArgumentNullException>().WithParameterName("prefix");
    }

    [TestMethod]
    public void ByResourceIdPrefix_ShouldThrowArgument_GivenEmptyPrefix()
    {
        var filter = () => SsoProviderFilter.ByResourceIdPrefix(string.Empty);
        filter.Should().ThrowExactly<ArgumentException>().WithParameterName("prefix").WithMessage("A resource ID prefix cannot be empty.*",
            "the auth server ignores an empty resource_id_prefix and lists every provider: list without a filter for that");
    }
}
