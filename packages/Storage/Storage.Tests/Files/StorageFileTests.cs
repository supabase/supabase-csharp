using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Interfaces;
using FileOptions = Supabase.Storage.FileOptions;

namespace Storage.Tests.Files;

/// <summary>
/// End-to-end tests for a bucket's file API against a running local Supabase stack
/// (<c>supabase start</c>): plain and resumable uploads, cancellation and resume, downloads,
/// move/copy, public and signed URL generation, and the listing sort options.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class StorageFileTests : StorageE2EFixture
{
    private IStorageFileApi<FileObject> bucket = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var bucketId = await this.NewBucket(new BucketUpsertOptions { Public = true });
        this.bucket = this.Storage.From(bucketId);
    }

    [TestMethod]
    public async Task Upload_ShouldStoreFileAndReportProgress()
    {
        var progressed = new TaskCompletionSource<bool>();
        var name = $"{Guid.NewGuid()}.png";
        var imagePath = Path.Combine(BasePath(), "Assets", "supabase-csharp.png");
        await this.bucket.Upload(imagePath, name, null, (_, _) => progressed.TrySetResult(true));
        var list = await this.bucket.List();
        list!.Find(item => item.Name == name).Should().NotBeNull();
        (await progressed.Task).Should().BeTrue();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task UploadOrResume_ShouldStoreFileAndReportProgress_GivenLocalFile()
    {
        var progressed = new TaskCompletionSource<bool>();
        var name = $"{Guid.NewGuid()}.png";
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
        await File.WriteAllBytesAsync(tempFilePath, RandomBytes(2 * 1024 * 1024));
        try
        {
            var options = new FileOptions
            {
                Duplex = "duplex",
                Metadata = new Dictionary<string, string> { ["custom"] = "metadata", ["local_file"] = "local_file" },
                Headers = new Dictionary<string, string> { ["x-version"] = "123" }
            };
            await this.bucket.UploadOrResume(tempFilePath, name, options, (_, _) => progressed.TrySetResult(true));
            var list = await this.bucket.List();
            list!.Find(item => item.Name == name).Should().NotBeNull();
            (await progressed.Task).Should().BeTrue();
            await this.bucket.Remove(new List<string> { name });
        }
        finally
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }

    [TestMethod]
    public async Task UploadOrResume_ShouldStoreFileAndReportProgress_GivenBytes()
    {
        var progressed = new TaskCompletionSource<bool>();
        var name = $"{Guid.NewGuid()}.png";
        var options = new FileOptions
        {
            Duplex = "duplex",
            Metadata = new Dictionary<string, string> { ["custom"] = "metadata", ["local_file"] = "local_file" },
            Headers = new Dictionary<string, string> { ["x-version"] = "123" }
        };
        await this.bucket.UploadOrResume(RandomBytes(1024 * 1024), name, options, (_, _) => progressed.TrySetResult(true));
        var list = await this.bucket.List();
        list!.Find(item => item.Name == name).Should().NotBeNull();
        (await progressed.Task).Should().BeTrue();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task UploadOrResume_ShouldOverwriteExistingFile_GivenUpsert()
    {
        var progressed = new TaskCompletionSource<bool>();
        var name = $"{Guid.NewGuid()}.png";
        var data = RandomBytes(1024 * 1024);
        var options = new FileOptions
        {
            Duplex = "duplex",
            Metadata = new Dictionary<string, string> { ["custom"] = "metadata", ["local_file"] = "local_file" },
            Upsert = true
        };
        await this.bucket.UploadOrResume(data, name, options, (_, _) => progressed.TrySetResult(true));
        await this.bucket.UploadOrResume(data, name, options, (_, _) => progressed.TrySetResult(true));
        var list = await this.bucket.List();
        list!.Find(item => item.Name == name).Should().NotBeNull();
        (await progressed.Task).Should().BeTrue();
        await this.bucket.Remove(new List<string> { name });
    }

    // The interrupt-then-resume scenario is verified deterministically in the inner loop by
    // ResumableUploadContractTests. It cannot be a live E2E: cancelling a real upload mid-flight and
    // resuming races the server-side committed offset (fast machine cancels before any commit → works;
    // CI commits first → "Upload-Offset conflict"), which is non-deterministic by construction.

    [TestMethod]
    public async Task Upload_ShouldPersistMetadata_GivenFileOptions()
    {
        var progressed = new TaskCompletionSource<bool>();
        var name = $"{Guid.NewGuid()}.png";
        var imagePath = Path.Combine(BasePath(), "Assets", "supabase-csharp.png");
        var metadata = new Dictionary<string, string> { ["custom"] = "metadata", ["local_file"] = "local_file" };
        var options = new FileOptions
        {
            Duplex = "duplex",
            Metadata = metadata,
            Headers = new Dictionary<string, string> { ["x-version"] = "123" }
        };
        await this.bucket.Upload(imagePath, name, options, (_, _) => progressed.TrySetResult(true));
        var item = await this.bucket.Info(name);
        item!.Metadata.Should().Contain("custom", metadata["custom"])
            .And.Contain("local_file", metadata["local_file"]);
        (await progressed.Task).Should().BeTrue();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task Upload_ShouldStoreBytesAndReportProgress()
    {
        var progressed = new TaskCompletionSource<bool>();
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x0, 0x0 }, name, null, (_, _) => progressed.TrySetResult(true));
        var list = await this.bucket.List();
        list!.Find(item => item.Name == name).Should().NotBeNull();
        (await progressed.Task).Should().BeTrue();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task Upload_ShouldCancelAndStoreNothing_GivenCancelledToken()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
        var name = $"{Guid.NewGuid()}.bin";
        var act = () => this.bucket.Upload(RandomBytes(20 * 1024 * 1024), name, null, null, true, cts.Token);
        await act.Should().ThrowAsync<TaskCanceledException>();
        var list = await this.bucket.List();
        list!.Find(item => item.Name == name).Should().BeNull();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task Download_ShouldWriteFileToDisk()
    {
        var progressed = new TaskCompletionSource<bool>();
        var name = $"{Guid.NewGuid()}.png";
        var imagePath = Path.Combine(BasePath(), "Assets", "supabase-csharp.png");
        await this.bucket.Upload(imagePath, name);
        var downloadPath = Path.Combine(BasePath(), name);
        await this.bucket.Download(name, downloadPath, (_, _) => progressed.TrySetResult(true));
        (await progressed.Task).Should().BeTrue();
        File.Exists(downloadPath).Should().BeTrue();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task Download_ShouldCancelAndLeaveNoFile_GivenCancelledToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var name = $"{Guid.NewGuid()}.png";
        var imagePath = Path.Combine(BasePath(), "Assets", "supabase-csharp.png");
        await this.bucket.Upload(imagePath, name);
        var downloadPath = Path.Combine(BasePath(), name);
        var act = () => this.bucket.Download(name, downloadPath, null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(downloadPath).Should().BeFalse();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task Download_ShouldReturnTheStoredBytes()
    {
        var progressed = new TaskCompletionSource<bool>();
        var data = new byte[] { 0x0 };
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(data, name);
        var result = await this.bucket.Download(name, (_, _) => progressed.TrySetResult(true));
        (await progressed.Task).Should().BeTrue();
        result.Should().Equal(data);
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task Move_ShouldRenameTheFile()
    {
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        await this.bucket.Move(name, "new-file.bin");
        var items = await this.bucket.List();
        items!.Find(f => f.Name == "new-file.bin").Should().NotBeNull();
        items.Find(f => f.Name == name).Should().BeNull();
    }

    [TestMethod]
    public async Task Copy_ShouldDuplicateTheFileWithinTheBucket()
    {
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        await this.bucket.Copy(name, "new-file.bin");
        var items = await this.bucket.List();
        items!.Find(f => f.Name == "new-file.bin").Should().NotBeNull();
        items.Find(f => f.Name == name).Should().NotBeNull();
    }

    [TestMethod]
    public async Task Copy_ShouldDuplicateTheFileToAnotherBucket()
    {
        var destinationId = await this.NewBucket(new BucketUpsertOptions { Public = true });
        var localBucket = this.Storage.From(destinationId);
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        await this.bucket.Copy(name, "new-file.bin", new DestinationOptions { DestinationBucket = destinationId });
        var items = await this.bucket.List();
        var copied = await localBucket.List();
        copied!.Find(f => f.Name == "new-file.bin").Should().NotBeNull();
        items!.Find(f => f.Name == name).Should().NotBeNull();
    }

    [TestMethod]
    public async Task GetPublicUrl_ShouldReturnAUrl()
    {
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        this.bucket.GetPublicUrl(name, null).Should().NotBeNull();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task GetPublicUrl_ShouldAppendTheDownloadName_GivenDownloadOptions()
    {
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        var url = this.bucket.GetPublicUrl(name, null, new DownloadOptions { FileName = "custom-file.png" });
        url.Should().Contain("download=custom-file.png");
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task GetPublicUrl_ShouldAppendDownloadTrue_GivenTransformAndOriginalName()
    {
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        var url = this.bucket.GetPublicUrl(name, new TransformOptions { Height = 100, Width = 100 },
            DownloadOptions.UseOriginalFileName);
        url.Should().Contain("download=true");
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task CreateSignedUrl_ShouldReturnAnAbsoluteUrl()
    {
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        var url = await this.bucket.CreateSignedUrl(name, 3600);
        Uri.IsWellFormedUriString(url, UriKind.Absolute).Should().BeTrue();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task CreateSignedUrl_ShouldReturnAnAbsoluteUrl_GivenTransformOptions()
    {
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        var url = await this.bucket.CreateSignedUrl(name, 3600, new TransformOptions { Width = 100, Height = 100 });
        Uri.IsWellFormedUriString(url, UriKind.Absolute).Should().BeTrue();
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task CreateSignedUrl_ShouldAppendTheDownloadName_GivenDownloadOptions()
    {
        var name = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name);
        var url = await this.bucket.CreateSignedUrl(name, 3600, null, new DownloadOptions { FileName = "custom-file.png" });
        Uri.IsWellFormedUriString(url, UriKind.Absolute).Should().BeTrue();
        url.Should().Contain("download=custom-file.png");
        await this.bucket.Remove(new List<string> { name });
    }

    [TestMethod]
    public async Task CreateSignedUrls_ShouldReturnAbsoluteUrlsWithOriginalNames()
    {
        var name1 = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name1);
        var name2 = $"{Guid.NewGuid()}.bin";
        await this.bucket.Upload(new byte[] { 0x0, 0x1 }, name2);
        var urls = await this.bucket.CreateSignedUrls(new List<string> { name1, name2 }, 3600,
            DownloadOptions.UseOriginalFileName);
        urls.Should().NotBeNull();
        urls!.Should().OnlyContain(response =>
            Uri.IsWellFormedUriString(response.SignedUrl, UriKind.Absolute) && response.SignedUrl!.Contains("download=true"));
        await this.bucket.Remove(new List<string> { name1 });
    }

    [TestMethod]
    public async Task CreateUploadSignedUrl_ShouldReturnAnAbsoluteUrl()
    {
        var result = await this.bucket.CreateUploadSignedUrl("test.png");
        Uri.IsWellFormedUriString(result.SignedUrl.ToString(), UriKind.Absolute).Should().BeTrue();
    }

    [TestMethod]
    public async Task List_ShouldReturnFilesInInsertionOrder_GivenDefaults()
    {
        var names = await this.UploadThreeNumbered();
        var list = await this.bucket.List();
        list!.Select(item => item.Name).Should().Equal(names[0], names[1], names[2]);
    }

    [TestMethod]
    public async Task List_ShouldReturnFilesDescending_GivenOrderDesc()
    {
        var names = await this.UploadThreeNumbered();
        var options = new SearchOptions { SortBy = new SortBy { Order = "desc" } };
        var list = await this.bucket.List("", options);
        list!.Select(item => item.Name).Should().Equal(names[2], names[1], names[0]);
    }

    [TestMethod]
    public async Task List_ShouldReturnFilesInInsertionOrder_GivenCreatedAtColumn()
    {
        var names = await this.UploadThreeNumbered();
        var options = new SearchOptions { SortBy = new SortBy { Column = "created_at" } };
        var list = await this.bucket.List("", options);
        list!.Select(item => item.Name).Should().Equal(names[0], names[1], names[2]);
    }

    [TestMethod]
    public async Task List_ShouldReturnFilesDescending_GivenCreatedAtColumnAndOrderDesc()
    {
        var names = await this.UploadThreeNumbered();
        var options = new SearchOptions { SortBy = new SortBy { Column = "created_at", Order = "desc" } };
        var list = await this.bucket.List("", options);
        list!.Select(item => item.Name).Should().Equal(names[2], names[1], names[0]);
    }

    private async Task<string[]> UploadThreeNumbered()
    {
        var names = new[]
        {
            $"1-{Guid.NewGuid()}.bin",
            $"2-{Guid.NewGuid()}.bin",
            $"3-{Guid.NewGuid()}.bin"
        };
        foreach (var name in names)
            await this.bucket.Upload(new byte[] { 0x0, 0x0, 0x0 }, name);
        return names;
    }

    [TestMethod]
    public async Task ListV2Async_ShouldPageThroughEveryObjectUnderThePrefix()
    {
        await this.bucket.Upload(new byte[] { 1 }, "nested/a.txt");
        await this.bucket.Upload(new byte[] { 1 }, "nested/sub/b.txt");
        var first = await this.bucket.ListV2Async(new SearchV2Options { Prefix = "nested/", Limit = 1 });
        var second = await this.bucket.ListV2Async(new SearchV2Options { Prefix = "nested/", Cursor = first!.NextCursor });
        using (new AssertionScope())
        {
            first.HasNext.Should().BeTrue();
            second!.HasNext.Should().BeFalse();
            first.Objects.Concat(second.Objects).Select(item => item.Name).Should().BeEquivalentTo("nested/a.txt", "nested/sub/b.txt");
        }
    }

    [TestMethod]
    public async Task ListV2Async_ShouldStopAtSubFolders_GivenADelimiter()
    {
        await this.bucket.Upload(new byte[] { 1 }, "nested/a.txt");
        await this.bucket.Upload(new byte[] { 1 }, "nested/sub/b.txt");
        var page = await this.bucket.ListV2Async(new SearchV2Options { Prefix = "nested/", WithDelimiter = true });
        using (new AssertionScope())
        {
            var file = page!.Objects.Should().ContainSingle().Subject;
            file.Name.Should().Be("nested/a.txt");
            file.Key.Should().Be("a.txt");
            page.Folders.Should().ContainSingle().Which.Name.Should().Be("nested/sub/");
        }
    }

    private static byte[] RandomBytes(int length)
    {
        var data = new byte[length];
        new Random().NextBytes(data);
        return data;
    }

    private static string BasePath() =>
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!.Replace("file:", "");
}
