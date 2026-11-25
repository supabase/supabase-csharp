using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;

namespace Storage.Tests.PutVectors;

/// <summary>
/// Unit tests for the checks <c>Vectors.From(bucket).Index(name).PutVectorsAsync</c> makes before it touches the wire.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PutVectorsTests
{
    [TestMethod]
    public async Task PutVectorsAsync_ShouldThrowArgumentNullException_GivenNullVectors()
    {
        var sut = new Client("http://localhost:1/storage/v1");
        (await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").Index("docs-v1").PutVectorsAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("a null collection breaks the method's contract and cannot become a request"))
            .Which.ParamName.Should().Be("vectors");
    }

    [TestMethod]
    public async Task PutVectorsAsync_ShouldNotSendARequest_GivenNullVectors()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").Index("docs-v1").PutVectorsAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
        handler.ReceivedCalls().Should().BeEmpty("the argument check happens before any I/O");
    }
}
