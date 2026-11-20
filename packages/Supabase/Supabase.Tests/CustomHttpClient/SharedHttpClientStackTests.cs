using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Tests.Models;

namespace Supabase.Tests.CustomHttpClient;

/// <summary>
/// A client supplied through <see cref="SupabaseOptions.HttpClient"/> carries Database, Storage and Auth traffic to a live
/// local stack (client.request_configuration.custom_http_client).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SharedHttpClientStackTests
{
    private const string ServiceKey = "sb_secret_N7UND0UgjKTVK-Uodkm0Hg_xSvEMPvz";

    private sealed class PathRecordingHandler : DelegatingHandler
    {
        internal List<string> Paths { get; } = new();

        public PathRecordingHandler() : base(new HttpClientHandler())
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Paths.Add(request.RequestUri!.AbsolutePath);
            return base.SendAsync(request, cancellationToken);
        }
    }

    [TestMethod]
    public async Task Client_ShouldReachTheStackThroughTheSharedClient_GivenDatabaseStorageAndAuthCalls()
    {
        var handler = new PathRecordingHandler();
        using var http = new HttpClient(handler);
        var client = new Supabase.Client("http://localhost:54321", ServiceKey, new SupabaseOptions { HttpClient = http });
        await client.From<Channel>().Get();
        await client.Storage.ListBuckets();
        await client.AdminAuth(ServiceKey).ListUsers();
        handler.Paths.Select(path => path.Split('/')[1]).Distinct().Should().BeEquivalentTo(
            new[] { "rest", "storage", "auth" }, "database, storage and auth requests must all travel through the caller's client");
    }
}
