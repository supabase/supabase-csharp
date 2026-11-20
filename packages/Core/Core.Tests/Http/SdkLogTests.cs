using System;
using Core.Tests.Support;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Logging;

namespace Core.Tests.Http;

/// <summary>
/// What background work tells a logger: lifecycle events at Debug and failures at Warning, naming the component and the type of
/// the failure but never its message (client.observability.diagnostic_logging).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SdkLogTests
{
    [TestMethod]
    public void BackgroundWorkEvent_ShouldLogTheComponentAtDebug()
    {
        var capture = new LogCapture();
        capture.Logger.BackgroundWorkEvent("Supabase.Realtime", "connected");
        var record = capture.Records.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            record.Level.Should().Be(LogLevel.Debug);
            record.Message.Should().Be("Supabase.Realtime: connected");
        }
    }

    [TestMethod]
    public void BackgroundWorkFailed_ShouldLogTheFailureTypeAtWarning_GivenNoOtherReportingChannel()
    {
        var capture = new LogCapture();
        capture.Logger.BackgroundWorkFailed("Supabase.Gotrue", "Token refresh", new InvalidOperationException("refresh_token=secret"));
        var record = capture.Records.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            record.Level.Should().Be(LogLevel.Warning, "a failure with no caller to return an error to is the one case above debug");
            record.Message.Should().Be("Supabase.Gotrue: Token refresh failed with InvalidOperationException");
        }
    }

    [TestMethod]
    public void BackgroundWorkFailed_ShouldNotThrow_GivenTheLoggerThrows() =>
        FluentActions.Invoking(() => LogCapture.Throwing().BackgroundWorkFailed("Supabase.Gotrue", "Token refresh", new InvalidOperationException()))
            .Should().NotThrow("a logger that throws never changes the outcome of a call");

    [TestMethod]
    public void BackgroundWork_ShouldNotThrow_GivenNoLogger()
    {
        ILogger? logger = null;
        using (new AssertionScope())
        {
            FluentActions.Invoking(() => logger.BackgroundWorkEvent("Supabase.Gotrue", "x")).Should().NotThrow("without a logger the SDK stays silent");
            FluentActions.Invoking(() => logger.BackgroundWorkFailed("Supabase.Gotrue", "x", new InvalidOperationException())).Should().NotThrow();
        }
    }
}
