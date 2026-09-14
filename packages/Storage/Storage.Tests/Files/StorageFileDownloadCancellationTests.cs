using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;

namespace Storage.Tests.Files;

/// <summary>
/// Contract tests pinning that the file API threads the caller's <see cref="CancellationToken"/> all
/// the way through the download pipeline: onto the HTTP request itself, and onto the response-body copy
/// on the chunked (no <c>Content-Length</c>) path that the progress-reporting branch never exercises.
/// A stub <see cref="HttpMessageHandler"/> stands in for the download client, and each test observes the
/// token *at the point under test* rather than relying on the thrown exception — the body copy honoured
/// the token before these fixes too, so a bare "throws when cancelled" assertion cannot tell the fix
/// apart from the pre-existing behaviour.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageFileDownloadCancellationTests
{
    private const string Bucket = "bucket";

    private Client client = null!;
    private HttpClient? downloadClient;

    [TestCleanup]
    public void TestCleanup() => this.downloadClient?.Dispose();

    [TestMethod]
    public async Task Download_ShouldForwardTheTokenToTheRequest()
    {
        using var cts = new CancellationTokenSource();
        var requestSawLiveToken = false;
        this.UseHandler(new StubHandler((_, token) =>
        {
            cts.Cancel();
            requestSawLiveToken = token.IsCancellationRequested;
            token.ThrowIfCancellationRequested();
            return EmptyOk();
        }));
        var act = () => this.client.From(Bucket).Download("a.bin", onProgress: null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        requestSawLiveToken.Should().BeTrue(
            "the request must carry the caller's token so a stalled connection can be cancelled before any body arrives");
    }

    [TestMethod]
    public async Task Download_ShouldHonorCancellation_GivenResponseWithoutContentLength()
    {
        using var cts = new CancellationTokenSource();
        var bodyCopySawLiveToken = false;
        var body = new TokenObservingStream(token =>
        {
            cts.Cancel();
            bodyCopySawLiveToken = token.IsCancellationRequested;
            token.ThrowIfCancellationRequested();
        });
        this.UseHandler(new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) }));
        var act = () => this.client.From(Bucket).Download("a.bin", onProgress: null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        bodyCopySawLiveToken.Should().BeTrue(
            "the body copy must carry the token even when the server omits Content-Length and no progress handler is supplied");
    }

    private void UseHandler(HttpMessageHandler handler)
    {
        this.downloadClient = new HttpClient(handler);
        this.client = new Client("http://localhost/storage/v1",
            new ClientOptions { HttpDownloadClient = this.downloadClient },
            new Dictionary<string, string> { { "Authorization", "Bearer test-key" } });
    }

    private static HttpResponseMessage EmptyOk() =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request, cancellationToken));
    }

    /// <summary>
    /// A non-seekable response body (so the response carries no <c>Content-Length</c>) that reports the
    /// token handed to each read back to the test before signalling end-of-stream.
    /// </summary>
    private sealed class TokenObservingStream(Action<CancellationToken> onRead) : Stream
    {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            onRead(cancellationToken);
            return Task.FromResult(0);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            onRead(cancellationToken);
            return new ValueTask<int>(0);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
