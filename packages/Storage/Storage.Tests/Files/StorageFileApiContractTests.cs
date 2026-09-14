using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using FileOptions = Supabase.Storage.FileOptions;

namespace Storage.Tests.Files;

/// <summary>
/// Contract tests for the object-level HTTP calls a bucket's file API builds — list, info, signed
/// URLs, move/copy, remove, byte upload/download — asserting each request's path, method and body,
/// how responses are read (including the absolute URLs the SDK stitches onto signed paths), and that
/// a missing signed URL or a non-JSON error surfaces a <see cref="SupabaseStorageException"/>.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageFileApiContractTests
{
    private const string Bucket = "bucket";

    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.client = new Client($"{this.server.Url}/storage/v1", new Dictionary<string, string>
        {
            { "Authorization", "Bearer test-key" }
        });
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task List_ShouldPostToTheListPathWithThePrefix()
    {
        this.Respond($"/storage/v1/object/list/{Bucket}", "POST", 200, "[{\"name\":\"a.png\"}]");
        var files = await this.client.From(Bucket).List("folder");
        using (new AssertionScope())
        {
            files.Should().ContainSingle().Which.Name.Should().Be("a.png");
            var request = this.SingleRequest();
            request.Method.Should().Be("POST");
            request.Path.Should().Be($"/storage/v1/object/list/{Bucket}");
            request.Body.Should().Contain("\"prefix\":\"folder\"").And.Contain("\"limit\":100");
        }
    }

    [TestMethod]
    public async Task Info_ShouldGetTheInfoPath()
    {
        this.Respond($"/storage/v1/object/info/{Bucket}/a.png", "GET", 200, "{\"name\":\"a.png\"}");
        var info = await this.client.From(Bucket).Info("a.png");
        using (new AssertionScope())
        {
            info!.Name.Should().Be("a.png");
            this.SingleRequest().Path.Should().Be($"/storage/v1/object/info/{Bucket}/a.png");
        }
    }

    [TestMethod]
    public async Task CreateSignedUrl_ShouldPostToSignPathAndReturnAbsoluteUrl()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}/a.png", "POST", 200,
            "{\"signedURL\":\"/object/sign/bucket/a.png?token=abc\"}");
        var url = await this.client.From(Bucket).CreateSignedUrl("a.png", 3600);
        using (new AssertionScope())
        {
            url.Should().StartWith($"{this.server.Url}/storage/v1/object/sign/bucket/a.png");
            this.SingleRequest().Body.Should().Contain("\"expiresIn\":3600");
        }
    }

    [TestMethod]
    public async Task CreateSignedUrl_ShouldNotAppendTrailingQuestionMark_GivenNoDownloadOptions()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}/a.png", "POST", 200,
            "{\"signedURL\":\"/object/sign/bucket/a.png?token=abc\"}");
        var url = await this.client.From(Bucket).CreateSignedUrl("a.png", 3600);
        url.Should().Be($"{this.server.Url}/storage/v1/object/sign/bucket/a.png?token=abc");
    }

    [TestMethod]
    public async Task CreateSignedUrl_ShouldAppendDownloadWithAmpersand_GivenDownloadOptions()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}/a.png", "POST", 200,
            "{\"signedURL\":\"/object/sign/bucket/a.png?token=abc\"}");
        var url = await this.client.From(Bucket)
            .CreateSignedUrl("a.png", 3600, null, new DownloadOptions { FileName = "photo.png" });
        url.Should().Be($"{this.server.Url}/storage/v1/object/sign/bucket/a.png?token=abc&download=photo.png");
    }

    [TestMethod]
    public async Task CreateSignedUrl_ShouldIncludeTransform_GivenTransformOptions()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}/a.png", "POST", 200,
            "{\"signedURL\":\"/object/sign/bucket/a.png?token=abc\"}");
        await this.client.From(Bucket).CreateSignedUrl("a.png", 3600, new TransformOptions { Width = 100 });
        this.SingleRequest().Body.Should().Contain("transform").And.Contain("100");
    }

    [TestMethod]
    public async Task CreateSignedUrl_ShouldThrow_GivenEmptySignedUrl()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}/a.png", "POST", 200, "{\"signedURL\":\"\"}");
        var act = () => this.client.From(Bucket).CreateSignedUrl("a.png", 3600);
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task CreateSignedUrls_ShouldPostPathsAndPrefixEachSignedUrl()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}", "POST", 200,
            "[{\"signedURL\":\"/object/sign/bucket/a.png?token=abc\",\"path\":\"a.png\"}]");
        var urls = await this.client.From(Bucket).CreateSignedUrls(new List<string> { "a.png" }, 3600);
        using (new AssertionScope())
        {
            urls.Should().ContainSingle().Which.SignedUrl.Should()
                .StartWith($"{this.server.Url}/storage/v1/object/sign/bucket/a.png");
            this.SingleRequest().Body.Should().Contain("\"paths\":[\"a.png\"]");
        }
    }

    [TestMethod]
    public async Task CreateSignedUrls_ShouldNotAppendTrailingQuestionMark_GivenNoDownloadOptions()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}", "POST", 200,
            "[{\"signedURL\":\"/object/sign/bucket/a.png?token=abc\",\"path\":\"a.png\"}]");
        var urls = await this.client.From(Bucket).CreateSignedUrls(new List<string> { "a.png" }, 3600);
        urls.Should().ContainSingle().Which.SignedUrl.Should()
            .Be($"{this.server.Url}/storage/v1/object/sign/bucket/a.png?token=abc");
    }

    [TestMethod]
    public async Task CreateSignedUrls_ShouldAppendDownloadWithAmpersand_GivenDownloadOptions()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}", "POST", 200,
            "[{\"signedURL\":\"/object/sign/bucket/a.png?token=abc\",\"path\":\"a.png\"}]");
        var urls = await this.client.From(Bucket)
            .CreateSignedUrls(new List<string> { "a.png" }, 3600, new DownloadOptions { FileName = "photo.png" });
        urls.Should().ContainSingle().Which.SignedUrl.Should()
            .Be($"{this.server.Url}/storage/v1/object/sign/bucket/a.png?token=abc&download=photo.png");
    }

    [TestMethod]
    public async Task CreateSignedUrls_ShouldThrow_GivenEmptySignedUrl()
    {
        this.Respond($"/storage/v1/object/sign/{Bucket}", "POST", 200, "[{\"signedURL\":\"\",\"path\":\"a.png\"}]");
        var act = () => this.client.From(Bucket).CreateSignedUrls(new List<string> { "a.png" }, 3600);
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task Move_ShouldPostTheSourceAndDestinationKeys()
    {
        this.Respond("/storage/v1/object/move", "POST", 200, "{\"message\":\"ok\"}");
        (await this.client.From(Bucket).Move("a.png", "b.png")).Should().BeTrue();
        using (new AssertionScope())
        {
            var request = this.SingleRequest();
            request.Path.Should().Be("/storage/v1/object/move");
            request.Body.Should().Contain("\"sourceKey\":\"a.png\"").And.Contain("\"destinationKey\":\"b.png\"")
                .And.Contain($"\"bucketId\":\"{Bucket}\"");
        }
    }

    [TestMethod]
    public async Task Copy_ShouldPostToCopyWithTheDestinationBucket()
    {
        this.Respond("/storage/v1/object/copy", "POST", 200, "{\"message\":\"ok\"}");
        await this.client.From(Bucket).Copy("a.png", "b.png", new DestinationOptions { DestinationBucket = "other" });
        using (new AssertionScope())
        {
            var request = this.SingleRequest();
            request.Path.Should().Be("/storage/v1/object/copy");
            request.Body.Should().Contain("\"destinationBucket\":\"other\"");
        }
    }

    [TestMethod]
    public async Task Remove_ShouldDeleteWithThePrefixes()
    {
        this.Respond($"/storage/v1/object/{Bucket}", "DELETE", 200, "[{\"name\":\"a.png\"}]");
        await this.client.From(Bucket).Remove(new List<string> { "a.png" });
        using (new AssertionScope())
        {
            var request = this.SingleRequest();
            request.Method.Should().Be("DELETE");
            request.Path.Should().Be($"/storage/v1/object/{Bucket}");
            request.Body.Should().Contain("\"prefixes\":[\"a.png\"]");
        }
    }

    [TestMethod]
    public async Task CreateUploadSignedUrl_ShouldReturnTheTokenAndKey()
    {
        this.Respond($"/storage/v1/object/upload/sign/{Bucket}/a.png", "POST", 200,
            "{\"url\":\"/object/upload/sign/bucket/a.png?token=abc\"}");
        var signed = await this.client.From(Bucket).CreateUploadSignedUrl("a.png");
        using (new AssertionScope())
        {
            signed.Token.Should().Be("abc");
            signed.Key.Should().Be("a.png");
            signed.SignedUrl.IsAbsoluteUri.Should().BeTrue();
        }
    }

    [TestMethod]
    public async Task CreateUploadSignedUrl_ShouldThrow_GivenResponseWithoutToken()
    {
        this.Respond($"/storage/v1/object/upload/sign/{Bucket}/a.png", "POST", 200,
            "{\"url\":\"/object/upload/sign/bucket/a.png\"}");
        var act = () => this.client.From(Bucket).CreateUploadSignedUrl("a.png");
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task Upload_ShouldPostBytesToTheObjectPathAndReturnTheFinalPath()
    {
        this.Respond($"/storage/v1/object/{Bucket}/a.bin", "POST", 200, "{\"Key\":\"x\"}");
        var path = await this.client.From(Bucket).Upload(new byte[] { 0x1, 0x2 }, "a.bin");
        using (new AssertionScope())
        {
            path.Should().Be($"{Bucket}/a.bin");
            this.SingleRequest().Path.Should().Be($"/storage/v1/object/{Bucket}/a.bin");
        }
    }

    [TestMethod]
    public async Task Upload_ShouldComposeStorageHeadersFromFileOptions()
    {
        this.Respond($"/storage/v1/object/{Bucket}/a.bin", "POST", 200, "{\"Key\":\"x\"}");
        var options = new FileOptions
        {
            CacheControl = "7200",
            ContentType = "image/png",
            Upsert = true,
            Duplex = "Half",
            Metadata = new Dictionary<string, string> { ["k"] = "v" }
        };
        await this.client.From(Bucket).Upload(new byte[] { 0x1 }, "a.bin", options, inferContentType: false);
        var request = this.SingleRequest();
        using (new AssertionScope())
        {
            this.HeaderOf(request, "x-upsert").Should().Be("true");
            this.HeaderOf(request, "x-duplex").Should().Be("half");
            this.HeaderOf(request, "cache-control").Should().Be("max-age=7200");
            this.HeaderOf(request, "x-metadata").Should().NotBeNullOrEmpty();
        }
    }

    [TestMethod]
    public async Task Upload_ShouldReportProgress_GivenAProgressHandler()
    {
        this.Respond($"/storage/v1/object/{Bucket}/a.bin", "POST", 200, "{\"Key\":\"x\"}");
        var reported = new TaskCompletionSource<bool>();
        var onProgress = Substitute.For<EventHandler<float>>();
        onProgress.When(handler => handler.Invoke(Arg.Any<object>(), Arg.Any<float>()))
            .Do(_ => reported.TrySetResult(true));
        await this.client.From(Bucket).Upload(new byte[] { 0x1, 0x2, 0x3 }, "a.bin", onProgress: onProgress);
        (await Task.WhenAny(reported.Task, Task.Delay(2000))).Should().Be(reported.Task,
            "the upload must surface progress to the caller's handler");
        onProgress.Received().Invoke(Arg.Any<object>(), Arg.Any<float>());
    }

    [TestMethod]
    public async Task Download_ShouldGetBytesFromTheObjectPath()
    {
        var payload = Encoding.UTF8.GetBytes("file-bytes");
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/{Bucket}/a.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload));
        var bytes = await this.client.From(Bucket).Download("a.bin", (EventHandler<float>?) null);
        using (new AssertionScope())
        {
            bytes.Should().Equal(payload);
            this.SingleRequest().Path.Should().Be($"/storage/v1/object/{Bucket}/a.bin");
        }
    }

    [TestMethod]
    public async Task Download_ShouldSurfaceServiceCode_GivenJsonError()
    {
        const string body =
            "{\"statusCode\":\"404\",\"error\":\"not_found\",\"code\":\"NoSuchKey\",\"message\":\"Object not found\"}";
        this.Respond($"/storage/v1/object/{Bucket}/missing.bin", "GET", 404, body);
        var act = () => this.client.From(Bucket).Download("missing.bin", (EventHandler<float>?) null);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Code.Should().Be("NoSuchKey");
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
        }
    }

    [TestMethod]
    public async Task Download_ShouldSendTheCacheNonceInTheQuery_GivenACacheNonce()
    {
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/{Bucket}/a.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(Encoding.UTF8.GetBytes("file-bytes")));
        await this.client.From(Bucket).Download("a.bin", (EventHandler<float>?) null, cacheNonce: "nonce-123");
        this.SingleRequest().Query!.Should().ContainKey("cacheNonce")
            .WhoseValue.Should().Contain("nonce-123", "the nonce must ride the request so the CDN cache is bypassed");
    }

    [TestMethod]
    public async Task Download_ShouldSendTheCacheNonceInTheQuery_GivenACacheNonceAndLocalPath()
    {
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/{Bucket}/a.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(Encoding.UTF8.GetBytes("file-bytes")));
        var localPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        try
        {
            await this.client.From(Bucket).Download("a.bin", localPath, (EventHandler<float>?) null, cacheNonce: "nonce-123");
            this.SingleRequest().Query!.Should().ContainKey("cacheNonce")
                .WhoseValue.Should().Contain("nonce-123", "the to-disk path must carry the nonce just as the byte path does");
        }
        finally
        {
            if (File.Exists(localPath))
                File.Delete(localPath);
        }
    }

    [TestMethod]
    public async Task PurgeCache_ShouldDeleteTheCdnObjectPathWithNoBodyAndReturnTheMessage()
    {
        this.Respond($"/storage/v1/cdn/{Bucket}/a.png", "DELETE", 200, "{\"message\":\"success\"}");
        var response = await this.client.From(Bucket).PurgeCache("a.png", new PurgeCacheOptions { Transformations = true });
        using (new AssertionScope())
        {
            response!.Message.Should().Be("success");
            var request = this.SingleRequest();
            request.Method.Should().Be("DELETE");
            request.Path.Should().Be($"/storage/v1/cdn/{Bucket}/a.png");
            request.Body.Should().BeNullOrEmpty("options travel in the query string, so the purge carries no payload");
        }
    }

    [TestMethod]
    public async Task PurgeCache_ShouldEncodeDelimiters_GivenAKeyWithUrlDelimiters()
    {
        this.Respond("/storage/v1/cdn/*", "DELETE", 200, "{\"message\":\"success\"}");
        await this.client.From(Bucket).PurgeCache("folder/a?b#c.png");
        this.SingleRequest().AbsoluteUrl.Should().EndWith($"/storage/v1/cdn/{Bucket}/folder/a%3Fb%23c.png",
            "an unescaped '?' or '#' truncates the key into a query string or fragment");
    }

    [TestMethod]
    public async Task Upload_ShouldSurfaceStorageException_GivenNonJsonError()
    {
        const string body = "<html><head><title>413 Request Entity Too Large</title></head></html>";
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/{Bucket}/big.bin").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(413).WithHeader("Content-Type", "text/html").WithBody(body));
        var act = () => this.client.From(Bucket).Upload(new byte[] { 0x1 }, "big.bin");
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>(
            "an oversized upload returns a non-JSON gateway error that must not crash JSON parsing (issue #14)")).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(413);
            exception.Content.Should().Be(body);
            exception.Reason.Should().Be(FailureHint.Reason.EntityTooLarge);
        }
    }

    [TestMethod]
    public async Task List_ShouldSurfaceStorageException_GivenNonJsonError()
    {
        const string body = "502 Bad Gateway";
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/list/{Bucket}").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(502).WithBody(body));
        var act = () => this.client.From(Bucket).List();
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(502);
            exception.Content.Should().Be(body);
        }
    }

    [TestMethod]
    public async Task UploadToSignedUrl_ShouldForwardMetadataAndCustomHeaders_GivenFileOptions()
    {
        var signedUrl = new UploadSignedUrl(
            new Uri($"{this.server.Url}/storage/v1/object/upload/sign/{Bucket}/file.bin?token=abc"),
            "abc",
            "file.bin");
        this.Respond($"/storage/v1/object/upload/sign/{Bucket}/file.bin", "POST", 200, "{\"Key\":\"x\"}");
        var options = new FileOptions
        {
            ContentType = "image/png",
            Metadata = new Dictionary<string, string> { ["k"] = "v" },
            Headers = new Dictionary<string, string> { ["x-version"] = "123" }
        };
        await this.client.From(Bucket).UploadToSignedUrl(Encoding.UTF8.GetBytes("data"), signedUrl, options, inferContentType: false);
        var request = this.SingleRequest();
        using (new AssertionScope())
        {
            this.HeaderOf(request, "x-metadata").Should().NotBeNullOrEmpty("metadata must ride the signed-URL upload (issue #252)");
            this.HeaderOf(request, "x-version").Should().Be("123", "custom headers must merge into the signed-URL upload (issue #252)");
        }
    }

    [TestMethod]
    public async Task UploadToSignedUrlFromDisk_ShouldForwardMetadataAndCustomHeaders_GivenFileOptions()
    {
        var signedUrl = new UploadSignedUrl(
            new Uri($"{this.server.Url}/storage/v1/object/upload/sign/{Bucket}/file.bin?token=abc"),
            "abc",
            "file.bin");
        this.Respond($"/storage/v1/object/upload/sign/{Bucket}/file.bin", "POST", 200, "{\"Key\":\"x\"}");
        var localPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllBytesAsync(localPath, new byte[] { 0x1, 0x2 });
        try
        {
            var options = new FileOptions
            {
                ContentType = "image/png",
                Metadata = new Dictionary<string, string> { ["k"] = "v" },
                Headers = new Dictionary<string, string> { ["x-version"] = "123" }
            };
            await this.client.From(Bucket).UploadToSignedUrl(localPath, signedUrl, options, inferContentType: false);
            var request = this.SingleRequest();
            using (new AssertionScope())
            {
                this.HeaderOf(request, "x-metadata").Should().NotBeNullOrEmpty("metadata must ride the from-disk signed-URL upload (issue #252)");
                this.HeaderOf(request, "x-version").Should().Be("123", "custom headers must merge into the from-disk signed-URL upload (issue #252)");
            }
        }
        finally
        {
            if (File.Exists(localPath))
                File.Delete(localPath);
        }
    }

    private void Respond(string path, string method, int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingMethod(method))
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));

    private IRequestMessage SingleRequest() => this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;

    private string HeaderOf(IRequestMessage request, string name) =>
        request.Headers!.First(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Value[0];
}
