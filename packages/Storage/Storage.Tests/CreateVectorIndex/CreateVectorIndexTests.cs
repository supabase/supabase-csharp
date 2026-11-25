using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;
using Supabase.Storage.CreateVectorIndex;

namespace Storage.Tests.CreateVectorIndex;

/// <summary>
/// Unit tests for the checks <c>Vectors.From(bucket).CreateIndexAsync</c> makes before it touches the wire.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class CreateVectorIndexTests
{
    [TestMethod]
    public void NewVectorIndex_ShouldDefaultToFloat32()
    {
        new NewVectorIndex { IndexName = "docs-v1", Dimension = 3, DistanceMetric = VectorDistanceMetric.Cosine }
            .DataType.Should().Be(VectorDataType.Float32, "float32 is the only type the service supports");
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldThrowArgumentNullException_GivenNoIndex()
    {
        var sut = new Client("http://localhost:1/storage/v1");
        (await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").CreateIndexAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is nothing to create")).Which.ParamName.Should().Be("index");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task CreateIndexAsync_ShouldThrowArgumentException_GivenABlankIndexName(string? name)
    {
        var sut = new Client("http://localhost:1/storage/v1");
        var thrown = await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").CreateIndexAsync(
                new NewVectorIndex { IndexName = name!, Dimension = 3, DistanceMetric = VectorDistanceMetric.Cosine }))
            .Should().ThrowAsync<ArgumentException>("a blank index name can never address an index");
        using (new AssertionScope())
        {
            thrown.Which.ParamName.Should().Be("IndexName");
            thrown.Which.Message.Should().Contain("null, empty or whitespace", "the caller needs to know why the name was refused");
        }
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldNotSendARequest_GivenABlankIndexName()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").CreateIndexAsync(
            new NewVectorIndex { IndexName = " ", Dimension = 3, DistanceMetric = VectorDistanceMetric.Cosine })).Should().ThrowAsync<ArgumentException>();
        handler.ReceivedCalls().Should().BeEmpty("validation happens before any I/O");
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldNotCheckDimension_GivenZero()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").CreateIndexAsync(
            new NewVectorIndex { IndexName = "docs-v1", Dimension = 0, DistanceMetric = VectorDistanceMetric.Cosine }))
            .Should().NotThrowAsync<ArgumentException>("the service owns dimension validation");
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldThrowArgumentOutOfRangeException_GivenAnUnknownDistanceMetric()
    {
        var sut = new Client("http://localhost:1/storage/v1");
        (await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").CreateIndexAsync(
                new NewVectorIndex { IndexName = "docs-v1", Dimension = 3, DistanceMetric = (VectorDistanceMetric) 99 }))
            .Should().ThrowAsync<ArgumentOutOfRangeException>("an enum value the SDK cannot map to a service value must not be guessed"))
            .Which.Message.Should().Contain("distance metric", "the caller needs to know which setting was refused");
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldThrowArgumentOutOfRangeException_GivenAnUnknownDataType()
    {
        var sut = new Client("http://localhost:1/storage/v1");
        (await FluentActions.Awaiting(() => sut.Vectors.From("embeddings-prod").CreateIndexAsync(
                new NewVectorIndex { IndexName = "docs-v1", Dimension = 3, DistanceMetric = VectorDistanceMetric.Cosine, DataType = (VectorDataType) 99 }))
            .Should().ThrowAsync<ArgumentOutOfRangeException>("an enum value the SDK cannot map to a service value must not be guessed"))
            .Which.Message.Should().Contain("data type", "the caller needs to know which setting was refused");
    }
}
