using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Core.Tests.Support;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Core.Http;

namespace Core.Tests.Http;

/// <summary>
/// What <see cref="RetryExecutor"/> tells a logger: the outcome and timing of each attempt and each retry decision, at Debug,
/// without credentials, query strings or bodies, and never at the cost of the call (client.observability.diagnostic_logging).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class RetryExecutorLoggingTests
{
    private const string SecretQuery = "access_token=secret-token";

    private static readonly RetryOptions Fast = new() { MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(5) };

    private static readonly RetryOptions Delayed = new() { MaxRetries = 2, BaseDelay = TimeSpan.FromMilliseconds(30), MaxDelay = TimeSpan.FromMilliseconds(30), UseJitter = false };

    private static HttpRequestMessage Get() => new(HttpMethod.Get, $"http://localhost/rest/v1/todos?{SecretQuery}");

    private static async Task Send(HttpMessageHandler handler, ILogger? logger, RetryOptions? retry = null)
    {
        using var client = new HttpClient(handler);
        (await RetryExecutor.SendAsync(client, Get, retry ?? new RetryOptions(), TracePropagation.Disabled, logger, CancellationToken.None)).Dispose();
    }

    [TestMethod]
    public async Task SendAsync_ShouldLogTheOutcomeAtDebug_GivenAResponse()
    {
        var capture = new LogCapture();
        await Send(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)), capture.Logger);
        var record = capture.Records.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            record.Level.Should().Be(LogLevel.Debug, "routine request outcomes are debug diagnostics");
            record.EventName.Should().Be("RequestCompleted");
            record.Message.Should().StartWith("GET http://localhost/rest/v1/todos responded 200 in ");
        }
    }

    [TestMethod]
    public async Task SendAsync_ShouldLogTheTimeTheAttemptTook()
    {
        var capture = new LogCapture();
        await Send(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)), capture.Logger);
        var elapsedMs = double.Parse(capture.Records.Single().Message.Split(" in ")[1].Replace(" ms", string.Empty), System.Globalization.CultureInfo.InvariantCulture);
        elapsedMs.Should().BeInRange(0, 10_000, "the elapsed time is measured from the start of the attempt");
    }

    [TestMethod]
    public async Task SendAsync_ShouldNeverLogTheQueryString()
    {
        var capture = new LogCapture();
        await Send(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)), capture.Logger);
        capture.Records.Should().NotContain(record => record.Message.Contains("secret-token") || record.Message.Contains("?"),
            "query strings carry credentials and are never logged");
    }

    [TestMethod]
    public async Task SendAsync_ShouldLogTheFailureTypeOnly_GivenATransportFailure()
    {
        var capture = new LogCapture();
        var act = () => Send(new StubHandler(_ => throw new HttpRequestException("connection refused: secret-detail")), capture.Logger);
        await act.Should().ThrowAsync<HttpRequestException>();
        var record = capture.Records.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            record.EventName.Should().Be("RequestFailed");
            record.Message.Should().Contain("HttpRequestException").And.NotContain("secret-detail", "exception messages can carry credentials");
        }
    }

    [TestMethod]
    public async Task SendAsync_ShouldLogEachRetryDecision_GivenRetryableStatuses()
    {
        var capture = new LogCapture();
        var responses = new Queue<HttpStatusCode>([HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK]);
        await Send(new StubHandler(_ => new HttpResponseMessage(responses.Dequeue())), capture.Logger, Fast);
        capture.Records.Where(record => record.EventName == "RetryScheduled").Select(record => record.Message[..record.Message.IndexOf(" in ", StringComparison.Ordinal)])
            .Should().Equal(
                "GET http://localhost/rest/v1/todos will be retried (attempt 1) after 503",
                "GET http://localhost/rest/v1/todos will be retried (attempt 2) after 503");
    }

    [TestMethod]
    public async Task SendAsync_ShouldLogTheExceptionTypeAsTheRetryReason_GivenATransportFailure()
    {
        var capture = new LogCapture();
        var attempts = 0;
        await Send(new StubHandler(_ => ++attempts == 1 ? throw new HttpRequestException("reset") : new HttpResponseMessage(HttpStatusCode.OK)), capture.Logger, Fast);
        capture.Records.Single(record => record.EventName == "RetryScheduled").Message.Should().Contain("after HttpRequestException");
    }

    [TestMethod]
    public async Task SendAsync_ShouldWriteNothing_GivenTheLoggerIgnoresDebug()
    {
        var capture = new LogCapture(LogLevel.Warning);
        await Send(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)), capture.Logger);
        capture.Records.Should().BeEmpty("a logger that does not accept Debug receives nothing");
    }

    [TestMethod]
    public async Task SendAsync_ShouldSucceed_GivenNoLogger()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await Send(handler, null);
        handler.CallCount.Should().Be(1, "without a logger the SDK stays silent and sends as usual");
    }

    [TestMethod]
    public async Task SendAsync_ShouldReturnTheResponse_GivenTheLoggerThrows()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await Send(handler, LogCapture.Throwing());
        handler.CallCount.Should().Be(1, "a logger that throws never changes the outcome of a call");
    }

    [TestMethod]
    public async Task SendAsync_ShouldReturnTheResponse_GivenTheLoggerThrowsWhenAskedIfEnabled()
    {
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(_ => throw new InvalidOperationException("the logger is broken"));
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await Send(handler, logger);
        handler.CallCount.Should().Be(1, "a logger that throws never changes the outcome of a call");
    }

    [TestMethod]
    public void SendAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        var responses = new Queue<HttpStatusCode>([HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK]);
        using var client = new HttpClient(new SlowHandler(() => responses.Dequeue()));
        BlockingCaller.Completes(() => RetryExecutor.SendAsync(client, Get, Delayed, TracePropagation.Disabled, new LogCapture().Logger, CancellationToken.None))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    [TestMethod]
    public async Task SendAsync_ShouldDisposeTheResponseItRetries()
    {
        var disposed = new List<bool>();
        var attempts = 0;
        await Send(new StubHandler(_ => ++attempts == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new DisposalTrackingContent(disposed) }
            : new HttpResponseMessage(HttpStatusCode.OK)), null, Fast);
        disposed.Should().Equal([true], "the response of an attempt that is retried is released before the next one");
    }

    private sealed class DisposalTrackingContent(List<bool> disposed) : HttpContent
    {
        protected override Task SerializeToStreamAsync(System.IO.Stream stream, System.Net.TransportContext? context) => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            disposed.Add(true);
            base.Dispose(disposing);
        }
    }

    private sealed class SlowHandler(Func<HttpStatusCode> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(respond());
        }
    }

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
