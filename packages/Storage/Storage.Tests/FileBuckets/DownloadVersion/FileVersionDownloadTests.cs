using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.FileBuckets.DownloadVersion;

/// <summary>
/// End-to-end tests for <c>DownloadVersionAsync</c> against the local stack, on a uniquely named private bucket
/// that <see cref="StorageE2EFixture"/> tears down.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class FileVersionDownloadTests : StorageE2EFixture
{
    [TestMethod]
    public async Task DownloadVersionAsync_ShouldReturnTheBytesOfThatVersionUntilItIsOverwritten()
    {
        var bucket = this.Storage.From(await this.NewBucket());
        var name = $"{Guid.NewGuid()}.txt";
        var original = Encoding.UTF8.GetBytes("first version");
        await bucket.Upload(original, name);
        var version = (await bucket.Info(name))!.Version;
        (await bucket.DownloadVersionAsync(name, version)).Should().Equal(original);
        await bucket.Upload(Encoding.UTF8.GetBytes("second version"), name, new FileOptions { Upsert = true });
        (await FluentActions.Awaiting(() => bucket.DownloadVersionAsync(name, version))
            .Should().ThrowAsync<SupabaseStorageException>("an unversioned bucket keeps no copy of an overwritten version"))
            .Which.Reason.Should().Be(FailureHint.Reason.NotFound);
    }
}
