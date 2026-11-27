using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core;
using Supabase.Tests.Models;

namespace Supabase.Tests.Observability;

/// <summary>
/// Through the umbrella client, every service request carries the umbrella's <c>X-Client-Info</c>, or the value the
/// application set in <see cref="SupabaseOptions.Headers"/> in any casing, with <c>typed-api</c> added to the
/// requests built with the typed Database API (client.observability.client_info).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ClientInfoWiringTests
{
    private const string SuppliedValue = "my-lib/1.0";
    private const string TypedApi = "; features=typed-api";

    private static readonly string UmbrellaValue = Util.GetAssemblyVersion(typeof(Client));

    [TestMethod]
    public async Task Client_ShouldIdentifyTheUmbrellaOnEveryService()
    {
        var handler = new RecordingHandler();
        await CallEveryService(new SupabaseOptions { HttpClient = new HttpClient(handler) });
        handler.ClientInfo.Should().Equal(UmbrellaValue, $"{UmbrellaValue}{TypedApi}", UmbrellaValue, UmbrellaValue, UmbrellaValue);
    }

    [TestMethod]
    public async Task Client_ShouldSendTheSuppliedValueToEveryService_GivenGlobalHeaderInAnyCasing()
    {
        var handler = new RecordingHandler();
        var options = new SupabaseOptions { HttpClient = new HttpClient(handler), Headers = { ["x-client-info"] = SuppliedValue } };
        await CallEveryService(options);
        handler.ClientInfo.Should().Equal([SuppliedValue, $"{SuppliedValue}{TypedApi}", SuppliedValue, SuppliedValue, SuppliedValue],
            "an X-Client-Info set through the global headers replaces the SDK's on every request");
    }

    private static async Task CallEveryService(SupabaseOptions options)
    {
        var client = new Client("http://localhost", "test-key", options);
        await client.Auth.Settings();
        await client.From<Channel>().Get();
        await client.Rpc("add_one", null);
        await client.Storage.ListBuckets();
        await client.Functions.Invoke("hello");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<string> clientInfo = new();

        internal IEnumerable<string> ClientInfo => this.clientInfo;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.clientInfo.AddRange(request.Headers.GetValues("X-Client-Info"));
            var body = request.RequestUri!.AbsolutePath.StartsWith("/auth") ? "{}" : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
