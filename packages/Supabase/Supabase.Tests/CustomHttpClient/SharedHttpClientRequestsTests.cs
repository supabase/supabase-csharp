using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Tests.Models;

namespace Supabase.Tests.CustomHttpClient;

/// <summary>
/// Requests of every sub-client that supports it travel through the supplied client and carry the project's
/// <c>apikey</c> and <c>Authorization</c> headers, without the client's own default headers being changed
/// (client.request_configuration.custom_http_client).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SharedHttpClientRequestsTests
{
    private const string ApiKey = "sb_publishable_test-key";

    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        }
    }

    private static async Task<(RecordingHandler Handler, HttpClient Http)> UseSharedClient(
        string key, System.Func<Supabase.Client, Task> call)
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        await call(new Supabase.Client("http://localhost", key, new SupabaseOptions { HttpClient = http }));
        return (handler, http);
    }

    [TestMethod]
    public async Task Client_ShouldSendDatabaseAndStorageRequestsThroughTheSharedClient()
    {
        var (handler, _) = await UseSharedClient(ApiKey, async client =>
        {
            await client.From<Channel>().Get();
            await client.Storage.ListBuckets();
        });
        handler.Requests.Select(request => request.RequestUri!.AbsolutePath).Should().BeEquivalentTo(
            new[] { "/rest/v1/channels", "/storage/v1/bucket" },
            "both services must use the caller's client");
    }

    [TestMethod]
    public async Task Client_ShouldInjectTheApiKeyOnEachRequestOnly()
    {
        var (handler, http) = await UseSharedClient(ApiKey, client => client.Storage.ListBuckets());
        using (new AssertionScope())
        {
            handler.Requests.Single().Headers.GetValues("apikey").Should().ContainSingle().Which.Should().Be(ApiKey, "the SDK injects the key per request");
            handler.Requests.Single().Headers.Contains("Authorization").Should().BeTrue("the SDK injects authorization per request");
            http.DefaultRequestHeaders.Contains("apikey").Should().BeFalse("the caller's client is never mutated");
        }
    }

    [TestMethod]
    public async Task Client_ShouldKeepProjectsApartOnOneSharedClient_GivenTwoApiKeys()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        await new Supabase.Client("http://localhost", "sb_publishable_first", new SupabaseOptions { HttpClient = http }).Storage.ListBuckets();
        await new Supabase.Client("http://localhost", "sb_publishable_second", new SupabaseOptions { HttpClient = http }).Storage.ListBuckets();
        handler.Requests.Select(request => request.Headers.GetValues("apikey").Single()).Should().Equal(
            "sb_publishable_first", "sb_publishable_second");
    }
}
