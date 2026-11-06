using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;

namespace Realtime.Tests.HttpSend;

/// <summary>
///     A broadcast sent through the REST API reaching a subscribed channel on the live stack.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class HttpSendRelayTests
{
    private IRealtimeClient<RealtimeSocket, RealtimeChannel> socketClient = null!;

    [TestInitialize]
    public async Task InitializeTest()
    {
        this.socketClient = Helpers.SocketClient();
        await this.socketClient.ConnectAsync();
    }

    [TestCleanup]
    public void CleanupTest() => this.socketClient.Disconnect();

    [TestMethod]
    public async Task HttpSendAsync_ShouldReachSubscribers()
    {
        var received = new TaskCompletionSource<BaseBroadcast?>();
        var channel = this.socketClient.Channel($"http-send-{Guid.NewGuid()}");
        channel.Register<BaseBroadcast>().AddBroadcastEventHandler((_, message) => received.TrySetResult(message));
        await channel.Subscribe();
        var id = Guid.NewGuid().ToString();
        await channel.HttpSendAsync("probe", new { id });
        var broadcast = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        broadcast!.Event.Should().Be("probe");
        broadcast.Payload!["id"].ToString().Should().Be(id);
    }
}
