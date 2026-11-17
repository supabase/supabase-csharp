using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;

namespace Gotrue.Tests.Support;

/// <summary>An <see cref="ILogger"/> substitute that keeps every record written to it as its level, event name and rendered message.</summary>
internal sealed class LogCapture
{
    internal sealed record Record(LogLevel Level, string EventName, string Message);

    private readonly List<Record> records = new();

    internal LogCapture(LogLevel minimumLevel = LogLevel.Debug)
    {
        this.Logger = Substitute.For<ILogger>();
        this.Logger.IsEnabled(Arg.Any<LogLevel>()).Returns(call => call.Arg<LogLevel>() >= minimumLevel);
        this.Logger.When(logger => logger.Log(Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<Arg.AnyType>(), Arg.Any<Exception?>(),
                Arg.Any<Func<Arg.AnyType, Exception?, string>>()))
            .Do(call => this.records.Add(Render(call)));
    }

    private static Record Render(CallInfo call)
    {
        var arguments = call.Args();
        var formatter = (Delegate) arguments[4]!;
        return new Record(call.Arg<LogLevel>(), call.Arg<EventId>().Name ?? string.Empty, (string) formatter.DynamicInvoke(arguments[2], arguments[3])!);
    }

    internal ILogger Logger { get; }

    internal IReadOnlyList<Record> Records => this.records;

    internal static ILogger Throwing()
    {
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        logger.When(substitute => substitute.Log(Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<Arg.AnyType>(), Arg.Any<Exception?>(),
                Arg.Any<Func<Arg.AnyType, Exception?, string>>()))
            .Do(_ => throw new InvalidOperationException("the logger is broken"));
        return logger;
    }
}
