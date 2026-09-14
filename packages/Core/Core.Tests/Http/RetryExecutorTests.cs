using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;

namespace Core.Tests.Http;

/// <summary>Covers <see cref="RetryExecutor"/>: retry/backoff policy, cancellation, and single-send behavior when disabled.</summary>
[TestClass]
[TestCategory("Unit")]
public class RetryExecutorTests
{
    private static readonly RetryOptions Fast = new() { MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(5) };

    [TestMethod]
    [DataRow(HttpStatusCode.OK, true)]
    [DataRow(HttpStatusCode.ServiceUnavailable, false)]
    [DataRow(HttpStatusCode.NotFound, true)]
    public async Task SendAsync_ShouldSendOnce_GivenNoRetryNeeded(HttpStatusCode status, bool retriesEnabled)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));
        using var client = new HttpClient(handler);
        var response = await RetryExecutor.SendAsync(client, Get, retriesEnabled ? Fast : new RetryOptions());
        response.StatusCode.Should().Be(status);
        handler.CallCount.Should().Be(1, "success, a non-retryable status, and disabled retries all send exactly once");
    }

    [TestMethod]
    public async Task SendAsync_ShouldRetryUntilSuccess_GivenRetryableStatusThenSuccess()
    {
        var responses = new Queue<HttpStatusCode>([HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK]);
        var handler = new StubHandler(_ => new HttpResponseMessage(responses.Dequeue()));
        using var client = new HttpClient(handler);
        var response = await RetryExecutor.SendAsync(client, Get, Fast);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(3, "a fresh request per attempt is required — HttpRequestMessage can't be sent twice");
    }

    [TestMethod]
    public async Task SendAsync_ShouldReturnLastResponse_GivenRetriesExhausted()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(handler);
        var response = await RetryExecutor.SendAsync(client, Get, Fast);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        handler.CallCount.Should().Be(3, "the initial attempt plus MaxRetries retries");
    }

    [TestMethod]
    public async Task SendAsync_ShouldRetryTransportException_GivenTransientFailureThenSuccess()
    {
        var attempts = 0;
        var handler = new StubHandler(_ => ++attempts == 1 ? throw new HttpRequestException("connection reset") : new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var response = await RetryExecutor.SendAsync(client, Get, Fast);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task SendAsync_ShouldRethrowTransportException_GivenRetriesExhausted()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection reset"));
        using var client = new HttpClient(handler);
        var act = () => RetryExecutor.SendAsync(client, Get, Fast);
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.CallCount.Should().Be(3);
    }

    [TestMethod]
    public async Task SendAsync_ShouldCapBackoffDelay_GivenDelayExceedsMaxDelay()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(handler);
        var options = new RetryOptions { MaxRetries = 4, BaseDelay = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(25), UseJitter = false };
        var stopwatch = Stopwatch.StartNew();
        await RetryExecutor.SendAsync(client, Get, options);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(250), "backoff should be capped at MaxDelay, not grow unbounded");
    }

    [TestMethod]
    public async Task SendAsync_ShouldStopRetrying_GivenCancellationDuringBackoffDelay()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(handler);
        var options = new RetryOptions { MaxRetries = 5, BaseDelay = TimeSpan.FromSeconds(30), MaxDelay = TimeSpan.FromSeconds(30) };
        var act = () => RetryExecutor.SendAsync(client, Get, options, cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));
        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.CallCount.Should().Be(1, "cancellation fires during the first backoff delay, before a second attempt");
    }

    private static HttpRequestMessage Get() => new(HttpMethod.Get, "http://localhost/");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(respond(request));
        }
    }
}
