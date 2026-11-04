using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;

namespace Realtime.Tests.Debugging;

/// <summary>
///     The debugger tells its registered listeners about every log line, until they are removed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DebuggerHandlerTests
{
    [TestInitialize]
    [TestCleanup]
    public void Reset() => Debugger.Instance.ClearDebugHandlers();

    [TestMethod]
    public void Log_ShouldCallTheListener_GivenItWasAdded()
    {
        var seen = new List<LogLine>();
        var line = new LogLine(new object(), "hello", new InvalidOperationException("boom"));
        Debugger.Instance.AddDebugHandler((sender, message, exception) => seen.Add(new LogLine(sender, message, exception)));

        Debugger.Instance.Log(line.Sender, line.Message, line.Error);

        seen.Should().Equal(new[] { line }, "the listener is called once per log line, with its sender, message and error");
    }

    [TestMethod]
    public void Log_ShouldNotCallTheListener_GivenItWasRemoved()
    {
        var calls = 0;
        IRealtimeDebugger.DebugEventHandler handler = (_, _, _) => calls++;
        Debugger.Instance.AddDebugHandler(handler);
        Debugger.Instance.RemoveDebugHandler(handler);

        Debugger.Instance.Log(new object(), "hello");

        calls.Should().Be(0, "a removed listener must not be called");
    }

    private sealed record LogLine(object Sender, string Message, Exception? Error);
}
