using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;

namespace Storage.Tests.Files;

/// <summary>
/// Contract tests pinning that the CDN purge calls thread the caller's <see cref="CancellationToken"/>
/// onto the outgoing HTTP request. A stub <see cref="HttpMessageHandler"/> stands in for the request
/// client and observes the token at the point under test, so the assertion distinguishes a forwarded
/// token from the default one a dropped argument would leave behind.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class PurgeCacheCancellationTests
{
    private const string Bucket = "bucket";

    private Client client = null!;
    private HttpClient? requestClient;

    [TestCleanup]
    public void TestCleanup() => this.requestClient?.Dispose();

    [TestMethod]
    public async Task PurgeCache_ShouldForwardTheTokenToTheRequest()
    {
        using var cts = new CancellationTokenSource();
        var act = () => this.client.From(Bucket).PurgeCache("a.png", options: null, cts.Token);
        (await this.TokenSeenBy(act, cts)).Should().BeTrue(
            "the object purge must carry the caller's token so an in-flight request can be cancelled");
    }

    [TestMethod]
    public async Task PurgeBucketCache_ShouldForwardTheTokenToTheRequest()
    {
        using var cts = new CancellationTokenSource();
        var act = () => this.client.PurgeBucketCache("photos", options: null, cts.Token);
        (await this.TokenSeenBy(act, cts)).Should().BeTrue(
            "the bucket purge must carry the caller's token so an in-flight request can be cancelled");
    }

    private async Task<bool> TokenSeenBy(Func<Task> act, CancellationTokenSource cts)
    {
        var requestSawLiveToken = false;
        this.UseHandler(new StubHandler((_, token) =>
        {
            cts.Cancel();
            requestSawLiveToken = token.IsCancellationRequested;
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
        }));
        await act.Should().ThrowAsync<OperationCanceledException>();
        return requestSawLiveToken;
    }

    private void UseHandler(HttpMessageHandler handler)
    {
        this.requestClient = new HttpClient(handler);
        this.client = new Client("http://localhost/storage/v1",
            new ClientOptions { HttpRequestClient = this.requestClient },
            new Dictionary<string, string> { { "Authorization", "Bearer test-key" } });
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request, cancellationToken));
    }
}
