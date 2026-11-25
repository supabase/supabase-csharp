using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Interfaces;

namespace Storage.Tests.Files;

/// <summary>
/// Covers <see cref="StorageFileApi.GetPublicUrl"/>, the pure URL builder: it targets the public
/// object path by default, switches to the image-render path only when a transform is actually
/// requested (an empty options object is not), and appends the download attribute when download
/// options ask for it.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PublicUrlTests
{
    private const string BaseUrl = "http://localhost/storage/v1";

    private static IStorageFileApi<FileObject> Bucket() =>
        new Client(BaseUrl, new Dictionary<string, string>()).From("bucket");

    [TestMethod]
    public void GetPublicUrl_ShouldTargetThePublicObjectPath()
    {
        var url = Bucket().GetPublicUrl("a.png", null);
        url.Should().StartWith($"{BaseUrl}/object/public/bucket/a.png");
    }

    [TestMethod]
    public void GetPublicUrl_ShouldUseTheRenderPath_GivenTransformOptions()
    {
        var url = Bucket().GetPublicUrl("a.png", new TransformOptions { Width = 100, Height = 100 });
        url.Should().Contain("/render/image/public/bucket/a.png").And.Contain("width=100");
    }

    [TestMethod]
    public void GetPublicUrl_ShouldTargetThePublicObjectPath_GivenEmptyTransformOptions()
    {
        var url = Bucket().GetPublicUrl("a.png", new TransformOptions());
        url.Should().StartWith($"{BaseUrl}/object/public/bucket/a.png").And.NotContain("/render/image/",
            "an empty TransformOptions requests no transform, so it must not route to the render endpoint (issue #37)");
    }

    [TestMethod]
    public void GetPublicUrl_ShouldEncodeEachSegment_GivenAKeyWithUrlDelimiters()
    {
        var plain = Bucket().GetPublicUrl("folder/a?b#c.png", null);
        var transformed = Bucket().GetPublicUrl("folder/a?b#c.png", new TransformOptions { Width = 100 });

        new Uri(plain).AbsolutePath.Should().EndWith("/object/public/bucket/folder/a%3Fb%23c.png");
        new Uri(transformed).AbsolutePath.Should().EndWith("/render/image/public/bucket/folder/a%3Fb%23c.png");
    }

    [TestMethod]
    [DataRow("../other/a.png")]
    [DataRow("folder/./a.png")]
    public void GetPublicUrl_ShouldThrow_GivenAKeyWithADotSegment(string key)
    {
        var plain = () => Bucket().GetPublicUrl(key, null);
        var transformed = () => Bucket().GetPublicUrl(key, new TransformOptions { Width = 100 });

        plain.Should().Throw<ArgumentException>().WithMessage("*'.' or '..' segment*");
        transformed.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GetPublicUrl_ShouldAppendDownloadName_GivenDownloadOptions()
    {
        var url = Bucket().GetPublicUrl("a.png", null, new DownloadOptions { FileName = "custom.png" });
        url.Should().Contain("download=custom.png");
    }
}
