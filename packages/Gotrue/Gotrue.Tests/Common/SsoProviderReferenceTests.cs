#region

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;

#endregion

namespace Gotrue.Tests.Common;

/// <summary>
///     The resource IDs a provider reference refuses before any request, those the auth server cannot address, and how
///     a reference reads in logs and the debugger.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SsoProviderReferenceTests
{
    [TestMethod]
    public void ByResourceId_ShouldThrowArgumentNull_GivenNullResourceId()
    {
        var reference = () => SsoProviderReference.ByResourceId(null!);
        reference.Should().ThrowExactly<ArgumentNullException>().WithParameterName("resourceId");
    }

    [TestMethod]
    public void ByResourceId_ShouldThrowArgument_GivenEmptyResourceId()
    {
        var reference = () => SsoProviderReference.ByResourceId(string.Empty);
        reference.Should().ThrowExactly<ArgumentException>().WithMessage("A resource ID cannot be empty.*",
            "resource_ with nothing after it addresses no provider");
    }

    [TestMethod]
    public void ByResourceId_ShouldThrowArgument_GivenResourceIdWithSlash()
    {
        var reference = () => SsoProviderReference.ByResourceId("acme/eu");
        reference.Should().ThrowExactly<ArgumentException>().WithMessage("*address it by id*",
            "the auth server matches routes on the raw path, so neither '/' nor '%2F' reaches it as part of the resource ID");
    }

    [TestMethod]
    public void ToString_ShouldNameTheId_GivenReferenceById()
    {
        var id = Guid.NewGuid();
        SsoProviderReference.ById(id).ToString().Should().Be($"SsoProviderReference {{ Id = {id} }}",
            "a reference in a log or the debugger should say which provider it addresses");
    }

    [TestMethod]
    public void ToString_ShouldNameTheUnescapedResourceId_GivenReferenceByResourceId() =>
        SsoProviderReference.ByResourceId("acme eu").ToString().Should().Be("SsoProviderReference { ResourceId = acme eu }",
            "the caller recognises the resource ID they registered, not its escaped route form");
}
