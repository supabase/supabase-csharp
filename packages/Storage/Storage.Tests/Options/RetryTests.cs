using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.Options;

/// <summary>
/// <see cref="ClientOptions.Retry"/> reaching the transport: metadata calls are retried, uploads are sent once.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RetryTests
{
    private WireMockServer server = null!;

    [TestInitialize]
    public void SetUp() => this.server = WireMockServer.Start();

    [TestCleanup]
    public void TearDown() => this.server.Stop();

    [TestMethod]
    public async Task ListBuckets_ShouldRetryUntilSuccess_GivenRetryableStatus()
    {
        this.server.Given(Request.Create().WithPath("/bucket").UsingGet())
            .InScenario("retry").WillSetStateTo("retried")
            .RespondWith(Response.Create().WithStatusCode(503));
        this.server.Given(Request.Create().WithPath("/bucket").UsingGet())
            .InScenario("retry").WhenStateIs("retried")
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[{\"id\":\"a\",\"name\":\"a\"}]"));

        var buckets = await this.RetryingClient().ListBuckets();

        buckets.Should().ContainSingle().Which.Name.Should().Be("a");
    }

    [TestMethod]
    public async Task Upload_ShouldNotRetry_GivenRetryOptions()
    {
        this.server.Given(Request.Create().WithPath("/object/photos/a.bin").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));

        var act = () => this.RetryingClient().From("photos").Upload(new byte[] { 1 }, "a.bin");

        await act.Should().ThrowAsync<SupabaseStorageException>();
        this.server.LogEntries.Should().ContainSingle("an upload is sent once, its body is not replayed");
    }

    private Client RetryingClient() =>
        new(this.server.Url!, new ClientOptions { Retry = new RetryOptions { MaxRetries = 1, BaseDelay = TimeSpan.FromMilliseconds(1) } });
}
