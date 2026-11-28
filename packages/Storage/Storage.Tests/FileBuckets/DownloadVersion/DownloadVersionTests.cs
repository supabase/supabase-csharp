using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;

namespace Storage.Tests.FileBuckets.DownloadVersion;

/// <summary>
/// Unit tests for the checks made before the wire when downloading a specific version of a file.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DownloadVersionTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task DownloadVersionAsync_ShouldThrowArgumentNullException_GivenNoVersionId() =>
        (await FluentActions.Awaiting(() => this.sut.From("avatars").DownloadVersionAsync("avatar.png", null!))
            .Should().ThrowAsync<ArgumentNullException>("without a version the current one would be downloaded"))
            .Which.ParamName.Should().Be("versionId");
}
