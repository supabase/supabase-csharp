using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;

namespace Realtime.Tests.Socket;

/// <summary>
///     Handlers may be added, removed and cleared while a message is being dispatched
///     (push replies and timeouts do this internally), without corrupting the handler list.
///     Probabilistic and slow, so it runs apart from the unit tests.
/// </summary>
[TestClass]
[TestCategory("Stress")]
public class HandlerConcurrencyTests
{
    private const int Iterations = 100_000;
    private const string Frame = """{"topic":"room","event":"broadcast","ref":"1","payload":{}}""";

    [TestMethod]
    public async Task MessageHandlers_ShouldSurviveConcurrentChanges_GivenDispatch()
    {
        var fake = new FakeWebsocketClient { OpenOnStart = true };
        var options = new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(fake) };
        _ = new Client("ws://localhost/socket", options); // installs the default message encoder and decoder on the options
        using var socket = new RealtimeSocket("ws://localhost/socket", options);
        await socket.Connect();
        var handlers = Enumerable.Range(0, 64)
            .Select(i => new IRealtimeSocket.MessageEventHandler((_, _) => GC.KeepAlive(i)))
            .ToArray();
        using var start = new ManualResetEventSlim();

        Task Worker(Action action) => Task.Factory.StartNew(() =>
        {
            start.Wait();
            action();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var workers = new[]
        {
            Worker(() => { for (var i = 0; i < Iterations; i++) socket.AddMessageReceivedHandler(handlers[i % handlers.Length]); }),
            Worker(() => { for (var i = 0; i < Iterations; i++) socket.RemoveMessageReceivedHandler(handlers[i % handlers.Length]); }),
            Worker(() =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    socket.ClearMessageReceivedHandlers();
                    socket.AddMessageReceivedHandler(handlers[0]);
                }
            }),
            Worker(() => { for (var i = 0; i < Iterations; i++) fake.Receive(Frame); }),
        };

        start.Set();
        Func<Task> act = () => Task.WhenAll(workers);

        await act.Should().NotThrowAsync("handlers change while push replies and timeouts dispatch");
        socket.Disconnect();
    }
}
