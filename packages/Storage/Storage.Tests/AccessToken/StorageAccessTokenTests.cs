using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Storage.Tests.Support;
using Supabase.Storage;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using FileOptions = Supabase.Storage.FileOptions;

namespace Storage.Tests.AccessToken;

/// <summary>
/// How every Storage request authenticates when <see cref="ClientOptions.RetrieveAccessToken"/> supplies the
/// token: bucket and object calls, uploads (direct and resumable) and downloads send the retrieved token in
/// place of the constructor's Authorization, an upload's own headers and a signed upload URL's token still
/// win, a null token leaves the headers unchanged, and the callback is awaited on every request.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageAccessTokenTests
{
    private const string Bucket = "bucket";
    private const string RetrievedBearer = "Bearer retrieved-token";
    private const string ResumablePath = "/storage/v1/upload/resumable";

    private static readonly byte[] Payload = [0x1, 0x2, 0x3];

    private WireMockServer server = null!;
    private Func<CancellationToken, Task<string?>> retrieveAccessToken = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        UploadMemoryCache.Clear();
        this.server = WireMockServer.Start();
        this.StubStorage();
        this.retrieveAccessToken = Substitute.For<Func<CancellationToken, Task<string?>>>();
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns("retrieved-token");
        this.client = this.ClientWith(new Dictionary<string, string> { { "Authorization", "Bearer constructor-key" } });
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.server.Stop();
        UploadMemoryCache.Clear();
    }

    [TestMethod]
    public async Task ListBuckets_ShouldSendTheRetrievedToken()
    {
        await this.client.ListBuckets();
        this.AuthorizationOf("GET").Should().Equal(RetrievedBearer);
    }

    [TestMethod]
    public async Task ListBuckets_ShouldReplaceTheConstructorAuthorization_GivenLowerCaseHeaderName()
    {
        var lowerCaseClient = this.ClientWith(new Dictionary<string, string> { { "authorization", "Bearer constructor-key" } });
        await lowerCaseClient.ListBuckets();
        this.AuthorizationOf("GET").Should().Equal([RetrievedBearer], "header names are case-insensitive, so the retrieved token replaces rather than joins it");
    }

    [TestMethod]
    public async Task ListBuckets_ShouldKeepTheConstructorAuthorization_GivenRetrieveAccessTokenReturnsNull()
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns((string?) null);
        await this.client.ListBuckets();
        this.AuthorizationOf("GET").Should().Equal(["Bearer constructor-key"], "a null token leaves the headers unchanged");
    }

    [TestMethod]
    public async Task ListBuckets_ShouldRetrieveTheTokenForEachRequest()
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns("first-token", "second-token");
        await this.client.ListBuckets();
        await this.client.ListBuckets();
        this.server.LogEntries.Select(entry => Authorization(entry.RequestMessage!).Single()).Should().Equal(["Bearer first-token", "Bearer second-token"], "the token is never cached");
    }

    [TestMethod]
    public async Task ListBuckets_ShouldThrowTheRetrievalFailure_GivenRetrieveAccessTokenThrows()
    {
        var failure = new InvalidOperationException("token provider unavailable");
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns<Task<string?>>(_ => throw failure);
        var act = () => this.client.ListBuckets();
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure, "the caller's own failure surfaces unwrapped");
    }

    [TestMethod]
    public async Task ListV2Async_ShouldPassTheCancellationTokenToRetrieveAccessToken()
    {
        using var cancellation = new CancellationTokenSource();
        await this.client.From(Bucket).ListV2Async(cancellationToken: cancellation.Token);
        await this.retrieveAccessToken.Received(1).Invoke(cancellation.Token);
    }

    [TestMethod]
    public async Task List_ShouldSendTheRetrievedToken()
    {
        await this.client.From(Bucket).List();
        this.AuthorizationOf("POST").Should().Equal(RetrievedBearer);
    }

    [TestMethod]
    public async Task Upload_ShouldSendTheRetrievedToken_GivenBytes()
    {
        await this.client.From(Bucket).Upload(Payload, "a.bin");
        this.AuthorizationOf("POST").Should().Equal(RetrievedBearer);
    }

    [TestMethod]
    public async Task Upload_ShouldSendTheRetrievedToken_GivenLocalFile()
    {
        var localPath = Path.GetTempFileName();
        try
        {
            await this.client.From(Bucket).Upload(localPath, "a.bin");
            this.AuthorizationOf("POST").Should().Equal(RetrievedBearer);
        }
        finally
        {
            File.Delete(localPath);
        }
    }

    [TestMethod]
    public async Task Upload_ShouldKeepTheFileOptionsAuthorization_GivenRetrieveAccessToken()
    {
        await this.client.From(Bucket).Upload(Payload, "a.bin", new FileOptions { Headers = new Dictionary<string, string> { { "Authorization", "Bearer upload-header" } } });
        this.AuthorizationOf("POST").Should().Equal(["Bearer upload-header"], "an Authorization header set on the upload is explicit and wins");
    }

    [TestMethod]
    public async Task UploadToSignedUrl_ShouldSendTheSignedUrlToken_GivenRetrieveAccessToken()
    {
        var signedUrl = new UploadSignedUrl(new Uri($"{this.server.Url}/storage/v1/object/upload/sign/{Bucket}/a.bin?token=signed-token"), "signed-token", "a.bin");
        await this.client.From(Bucket).UploadToSignedUrl(Payload, signedUrl);
        this.AuthorizationOf("POST").Should().Equal(["Bearer signed-token"], "a signed upload URL authorizes with its own token");
    }

    [TestMethod]
    public async Task UploadOrResume_ShouldSendTheRetrievedToken_GivenBytes()
    {
        await this.client.From(Bucket).UploadOrResume(Payload, "a.bin", new FileOptions());
        this.AuthorizationOf("POST").Should().Equal(RetrievedBearer);
    }

    [TestMethod]
    public async Task UploadOrResume_ShouldSendTheRetrievedToken_GivenLocalFile()
    {
        var localPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(localPath, Payload);
            await this.client.From(Bucket).UploadOrResume(localPath, "a.bin", new FileOptions());
            this.AuthorizationOf("POST").Should().Equal(RetrievedBearer);
        }
        finally
        {
            File.Delete(localPath);
        }
    }

    [TestMethod]
    public async Task Download_ShouldSendTheRetrievedToken_GivenBytesTarget()
    {
        await this.client.From(Bucket).Download("a.bin", (EventHandler<float>?) null);
        this.AuthorizationOf("GET").Should().Equal(RetrievedBearer);
    }

    [TestMethod]
    public async Task Download_ShouldSendTheRetrievedToken_GivenLocalFile()
    {
        var localPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        try
        {
            await this.client.From(Bucket).Download("a.bin", localPath, (EventHandler<float>?) null);
            this.AuthorizationOf("GET").Should().Equal(RetrievedBearer);
        }
        finally
        {
            File.Delete(localPath);
        }
    }

    [TestMethod]
    public void ListBuckets_ShouldComplete_GivenCallerBlockingAndTokenRetrievedLater() =>
        this.CompletesWhileCallerBlocks(() => this.client.ListBuckets(), RetrievedLater);

    [TestMethod]
    public void ListBuckets_ShouldComplete_GivenCallerBlockingAndTokenRetrievedAtOnce() =>
        this.CompletesWhileCallerBlocks(() => this.client.ListBuckets(), RetrievedAtOnce);

    [TestMethod]
    public void Upload_ShouldComplete_GivenBytesAndCallerBlockingAndTokenRetrievedLater() =>
        this.CompletesWhileCallerBlocks(() => this.client.From(Bucket).Upload(Payload, "a.bin"), RetrievedLater);

    [TestMethod]
    public void Upload_ShouldComplete_GivenLocalFileAndCallerBlockingAndTokenRetrievedLater()
    {
        var localPath = Path.GetTempFileName();
        try
        {
            this.CompletesWhileCallerBlocks(() => this.client.From(Bucket).Upload(localPath, "a.bin"), RetrievedLater);
        }
        finally
        {
            File.Delete(localPath);
        }
    }

    [TestMethod]
    public void UploadOrResume_ShouldComplete_GivenBytesAndCallerBlockingAndTokenRetrievedLater() =>
        this.CompletesWhileCallerBlocks(() => this.client.From(Bucket).UploadOrResume(Payload, "a.bin", new FileOptions()), RetrievedLater);

    [TestMethod]
    public void UploadOrResume_ShouldComplete_GivenLocalFileAndCallerBlockingAndTokenRetrievedLater()
    {
        var localPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(localPath, Payload);
            this.CompletesWhileCallerBlocks(() => this.client.From(Bucket).UploadOrResume(localPath, "a.bin", new FileOptions()), RetrievedLater);
        }
        finally
        {
            File.Delete(localPath);
        }
    }

    [TestMethod]
    public void Download_ShouldComplete_GivenBytesTargetAndCallerBlockingAndTokenRetrievedLater() =>
        this.CompletesWhileCallerBlocks(() => this.client.From(Bucket).Download("a.bin", (EventHandler<float>?) null), RetrievedLater);

    [TestMethod]
    public void Download_ShouldComplete_GivenBytesTargetAndCallerBlockingAndTokenRetrievedAtOnce() =>
        this.CompletesWhileCallerBlocks(() => this.client.From(Bucket).Download("a.bin", (EventHandler<float>?) null), RetrievedAtOnce);

    [TestMethod]
    public void Download_ShouldComplete_GivenLocalFileAndCallerBlockingAndTokenRetrievedLater() =>
        this.CompletesWhileCallerBlocksDownloadingToFile(RetrievedLater);

    [TestMethod]
    public void Download_ShouldComplete_GivenLocalFileAndCallerBlockingAndTokenRetrievedAtOnce() =>
        this.CompletesWhileCallerBlocksDownloadingToFile(RetrievedAtOnce);

    private void CompletesWhileCallerBlocksDownloadingToFile(Func<Task<string?>> retrieval)
    {
        var localPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        try
        {
            this.CompletesWhileCallerBlocks(() => this.client.From(Bucket).Download("a.bin", localPath, (EventHandler<float>?) null), retrieval);
        }
        finally
        {
            File.Delete(localPath);
        }
    }

    private void CompletesWhileCallerBlocks(Func<Task> call, Func<Task<string?>> retrieval)
    {
        this.retrieveAccessToken(Arg.Any<CancellationToken>()).Returns(_ => retrieval());
        BlockingCaller.Completes(call).Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static Task<string?> RetrievedLater() =>
        Task.Run<string?>(async () =>
        {
            await Task.Delay(1).ConfigureAwait(false);
            return "retrieved-token";
        });

    private static Task<string?> RetrievedAtOnce() => Task.FromResult<string?>("retrieved-token");

    private Client ClientWith(Dictionary<string, string> headers) =>
        new($"{this.server.Url}/storage/v1", new ClientOptions { RetrieveAccessToken = this.retrieveAccessToken }, headers);

    private void StubStorage()
    {
        var length = Payload.Length.ToString(CultureInfo.InvariantCulture);
        this.server.Given(Request.Create().UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/list/{Bucket}").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/list-v2/{Bucket}").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{\"hasNext\":false}"));
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/{Bucket}/a.bin").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{\"Key\":\"x\"}"));
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/upload/sign/{Bucket}/a.bin").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{\"Key\":\"x\"}"));
        this.server.Given(Request.Create().WithPath(ResumablePath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(201)
                .WithHeader("Location", $"{this.server.Url}{ResumablePath}/new-session")
                .WithHeader("Tus-Resumable", "1.0.0")
                .WithHeader("Upload-Offset", "0"));
        this.server.Given(Request.Create().UsingHead())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Tus-Resumable", "1.0.0")
                .WithHeader("Upload-Offset", "0")
                .WithHeader("Upload-Length", length));
        this.server.Given(Request.Create().UsingPatch())
            .RespondWith(Response.Create().WithStatusCode(204)
                .WithHeader("Tus-Resumable", "1.0.0")
                .WithHeader("Upload-Offset", length));
    }

    private IEnumerable<string> AuthorizationOf(string method) =>
        Authorization(this.server.LogEntries.Should().ContainSingle(entry => entry.RequestMessage!.Method == method).Which.RequestMessage!);

    private static IEnumerable<string> Authorization(IRequestMessage request) =>
        request.Headers!.Where(header => string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase)).SelectMany(header => header.Value);
}
