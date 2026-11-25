using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;

namespace Storage.Tests.QueryVectors;

/// <summary>
/// Unit tests for the checks <c>Vectors.From(bucket).Index(name).QueryVectorsAsync</c> makes before it touches the wire.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class QueryVectorsTests
{
    [TestMethod]
    public async Task QueryVectorsAsync_ShouldThrowArgumentNullException_GivenANullQueryVector()
    {
        var sut = new Client("http://localhost:1/storage/v1");
        (await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").Index("docs-v1").QueryVectorsAsync(null!, 5))
            .Should().ThrowAsync<ArgumentNullException>("a null query vector breaks the method's contract and cannot become a request"))
            .Which.ParamName.Should().Be("queryVector");
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldNotSendARequest_GivenANullQueryVector()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").Index("docs-v1").QueryVectorsAsync(null!, 5))
            .Should().ThrowAsync<ArgumentNullException>();
        handler.ReceivedCalls().Should().BeEmpty("the argument check happens before any I/O");
    }
}
