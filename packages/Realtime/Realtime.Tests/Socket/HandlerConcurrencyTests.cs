using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;

namespace Realtime.Tests.Socket;

/// <summary>
///     Handlers may be added, removed and cleared while a message is being dispatched
///     (push replies and timeouts do this internally), without corrupting the handler list.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class HandlerConcurrencyTests
{
    private const int Iterations = 100_000;

    [TestMethod]
    public async Task MessageHandlers_ShouldSurviveConcurrentChanges_GivenDispatch()
    {
        using var socket = new RealtimeSocket("ws://127.0.0.1/realtime/v1", new ClientOptions());
        var dispatch = typeof(RealtimeSocket).GetMethod("NotifyMessageReceived",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handlers = Enumerable.Range(0, 64)
            .Select(i => new IRealtimeSocket.MessageEventHandler((_, _) => GC.KeepAlive(i)))
            .ToArray();
        var message = new object[] { new SocketResponse() };
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
            Worker(() => { for (var i = 0; i < Iterations; i++) dispatch.Invoke(socket, message); }),
        };

        start.Set();
        await Task.WhenAll(workers);
    }
}
