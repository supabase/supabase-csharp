using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;

namespace Storage.Tests.DeleteVectorIndex;

/// <summary>
/// Unit tests for the checks <c>Vectors.From(bucket).DeleteIndexAsync</c> makes before it touches the wire.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeleteVectorIndexTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void From_ShouldThrowArgumentException_GivenABlankBucketName(string? name)
    {
        var sut = new Client("http://localhost:1/storage/v1");
        var thrown = FluentActions.Invoking(() => sut.Vectors.From(name!))
            .Should().Throw<ArgumentException>("a blank bucket name can never address a bucket");
        using (new AssertionScope())
        {
            thrown.Which.ParamName.Should().Be("vectorBucketName");
            thrown.Which.Message.Should().Contain("null, empty or whitespace", "the caller needs to know why the name was refused");
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task DeleteIndexAsync_ShouldThrowArgumentException_GivenABlankIndexName(string? name)
    {
        var sut = new Client("http://localhost:1/storage/v1");
        var thrown = await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").DeleteIndexAsync(name!))
            .Should().ThrowAsync<ArgumentException>("a blank index name can never address an index and must not reach the service");
        using (new AssertionScope())
        {
            thrown.Which.ParamName.Should().Be("indexName");
            thrown.Which.Message.Should().Contain("null, empty or whitespace", "the caller needs to know why the name was refused");
        }
    }

    [TestMethod]
    public async Task DeleteIndexAsync_ShouldNotSendARequest_GivenABlankIndexName()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").DeleteIndexAsync(" ")).Should().ThrowAsync<ArgumentException>();
        handler.ReceivedCalls().Should().BeEmpty("validation happens before any I/O");
    }
}
