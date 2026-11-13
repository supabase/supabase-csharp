#region

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;

#endregion

namespace Gotrue.Tests.Common;

/// <summary>
///     The metadata a provider is registered from: the URLs and XML refused before any request.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SamlMetadataTests
{
    [TestMethod]
    public void FromUrl_ShouldThrowArgumentNull_GivenNullUrl()
    {
        var metadata = () => SamlMetadata.FromUrl(null!);
        metadata.Should().ThrowExactly<ArgumentNullException>().WithParameterName("url");
    }

    [TestMethod]
    public void FromUrl_ShouldThrowArgument_GivenRelativeUrl()
    {
        var metadata = () => SamlMetadata.FromUrl(new Uri("/saml/metadata", UriKind.Relative));
        metadata.Should().ThrowExactly<ArgumentException>().WithMessage("The metadata URL must be absolute. (Parameter 'url')",
            "the auth server fetches the metadata itself, so it needs a URL that names the host");
    }

    [TestMethod]
    public void FromUrl_ShouldAcceptHttpUrl()
    {
        var metadata = () => SamlMetadata.FromUrl(new Uri("http://idp.acme.com/saml/metadata"));
        metadata.Should().NotThrow("enforcing HTTPS is the auth server's rule, which answers validation_failed");
    }

    [TestMethod]
    public void FromXml_ShouldThrowArgumentNull_GivenNullXml()
    {
        var metadata = () => SamlMetadata.FromXml(null!);
        metadata.Should().ThrowExactly<ArgumentNullException>().WithParameterName("xml");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \n\t")]
    public void FromXml_ShouldThrowArgument_GivenBlankXml(string xml)
    {
        var metadata = () => SamlMetadata.FromXml(xml);
        metadata.Should().ThrowExactly<ArgumentException>().WithMessage("The metadata XML cannot be empty. (Parameter 'xml')",
            "the auth server treats empty metadata_xml as absent and would answer that no metadata was given");
    }
}
