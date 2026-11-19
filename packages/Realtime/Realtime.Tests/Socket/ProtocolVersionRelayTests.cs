using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Supabase.Realtime;
using Supabase.Realtime.Models;

namespace Realtime.Tests.Socket;

/// <summary>
///     Two clients that speak protocol 2.0.0 against the live stack exchange a broadcast, with a JSON payload and
///     with a payload of bytes (realtime.configuration.binary_protocol).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ProtocolVersionRelayTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private static readonly byte[] Bytes = [0, 159, 146, 150, 255];

    private readonly string topic = $"protocol-v2-{Guid.NewGuid()}";
    private Client sender = null!;
    private Client receiver = null!;

    [TestInitialize]
    public async Task ConnectBothClients()
    {
        this.sender = Helpers.SocketClient(ProtocolVersion.V2);
        this.receiver = Helpers.SocketClient(ProtocolVersion.V2);
        await this.sender.ConnectAsync();
        await this.receiver.ConnectAsync();
    }

    [TestCleanup]
    public void DisconnectBothClients()
    {
        this.sender.Disconnect();
        this.receiver.Disconnect();
    }

    [TestMethod]
    public async Task Send_ShouldRelayTheBroadcast_GivenBothClientsSpeakProtocolV2()
    {
        var userId = Guid.NewGuid().ToString();
        var received = new TaskCompletionSource<string?>();
        var receiving = await this.Joined<BroadcastExample>(this.receiver);
        receiving.AddBroadcastEventHandler((_, _) => received.TrySetResult(receiving.Current()?.UserId));
        var sending = await this.Joined<BroadcastExample>(this.sender);
        await sending.Send("user", new BroadcastExample { UserId = userId });
        (await received.Task.WaitAsync(Patience)).Should().Be(userId, "clients on protocol 2.0.0 must exchange a broadcast like clients on 1.0.0");
    }

    [TestMethod]
    public async Task SendBinary_ShouldRelayTheBytes_GivenBothClientsSpeakProtocolV2()
    {
        var received = new TaskCompletionSource<byte[]?>();
        var receiving = await this.Joined<BaseBroadcast>(this.receiver);
        receiving.AddBroadcastEventHandler((_, _) => received.TrySetResult(receiving.Current()?.BinaryPayload));
        var sending = await this.Joined<BaseBroadcast>(this.sender);
        await sending.SendBinary("frame", Bytes);
        (await received.Task.WaitAsync(Patience)).Should().Equal(Bytes, "a broadcast of bytes must reach the other client byte for byte");
    }

    private async Task<RealtimeBroadcast<TBroadcast>> Joined<TBroadcast>(Client client) where TBroadcast : BaseBroadcast
    {
        var channel = client.Channel(this.topic);
        var broadcast = channel.Register<TBroadcast>(false, true);
        await channel.Subscribe();
        return broadcast;
    }
}
