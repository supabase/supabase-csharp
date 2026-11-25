using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;

namespace Storage.Tests.GetVectors;

/// <summary>
/// Unit tests for the checks <c>Vectors.From(bucket).Index(name).GetVectorsAsync</c> makes before it touches the wire.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class GetVectorsTests
{
    [TestMethod]
    public async Task GetVectorsAsync_ShouldThrowArgumentNullException_GivenNullKeys()
    {
        var sut = new Client("http://localhost:1/storage/v1");
        (await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").Index("docs-v1").GetVectorsAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("a null collection breaks the method's contract and cannot become a request"))
            .Which.ParamName.Should().Be("keys");
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldNotSendARequest_GivenNullKeys()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").Index("docs-v1").GetVectorsAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
        handler.ReceivedCalls().Should().BeEmpty("the argument check happens before any I/O");
    }
}
