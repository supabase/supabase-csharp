using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Connection;

/// <summary>
///     The client's connection lifecycle against a live socket: failing loudly when the server is
///     unreachable, staying failed on retry, reconnecting after a programmatic disconnect, and forwarding
///     custom headers on connect.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ClientConnectionTests
{
    [TestMethod]
    public async Task ConnectAsync_ShouldThrow_GivenUnreachableServer()
    {
        var client = new Client("ws://localhost");
        client.AddDebugHandler((_, message, _) => Debug.WriteLine(message));
        await Assert.ThrowsAsync<RealtimeException>(() => client.ConnectAsync());
    }

    [TestMethod]
    public async Task Connect_ShouldReportError_GivenUnreachableServer()
    {
        var client = new Client("ws://localhost");
        client.AddDebugHandler((_, message, _) => Debug.WriteLine(message));
        await Assert.ThrowsAsync<RealtimeException>(() =>
        {
            var tsc = new TaskCompletionSource();
#pragma warning disable CS0618 // pins the still-shipping obsolete callback Connect's error path
            client.Connect((_, exception) =>
            {
                if (exception != null)
                    tsc.SetException(exception);
            });
#pragma warning restore CS0618
            return tsc.Task;
        });
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldStayFailed_GivenRepeatedAttempts()
    {
        var client = new Client("ws://localhost");
        client.AddDebugHandler((_, message, _) => Debug.WriteLine(message));
        await Assert.ThrowsAsync<RealtimeException>(client.ConnectAsync);
        await Assert.ThrowsAsync<RealtimeException>(client.ConnectAsync);
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldReconnect_GivenProgrammaticDisconnect()
    {
        var client = Helpers.SocketClient();
        await client.ConnectAsync();
        client.Disconnect();
        await client.ConnectAsync();
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldForwardCustomHeaders()
    {
        var client = Helpers.SocketClient();
        client.GetHeaders = () => new Dictionary<string, string> { { "testing", "123" } };
        await client.ConnectAsync();
        Assert.IsNotNull(client.Socket);
        Assert.IsNotNull(client.Socket.GetHeaders);
        Assert.AreEqual("123", client.Socket.GetHeaders!()["testing"]);
    }

    [TestMethod]
    public async Task ProbeConnection_ShouldGetAHeartbeatReplyBeforeTheNextInterval_GivenALiveServer()
    {
        var patience = TimeSpan.FromSeconds(5);
        var answers = new SemaphoreSlim(0);
        var client = Helpers.SocketClient();
        client.AddHeartbeatHandler((_, status, _) =>
        {
            if (status == HeartbeatStatus.Ok) answers.Release();
        });
        await client.ConnectAsync();
        await answers.WaitAsync(patience);
        client.ProbeConnection();
        (await answers.WaitAsync(patience)).Should().BeTrue("the probe does not wait for the 20 second heartbeat interval");
    }
}
