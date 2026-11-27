using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;

namespace Storage.Tests.VectorBuckets.GetBucket;

/// <summary>
/// Unit tests for the checks <c>Vectors.GetVectorBucketAsync</c> makes before it touches the wire.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class GetVectorBucketTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GetVectorBucketAsync_ShouldThrowArgumentException_GivenABlankBucketName(string? name)
    {
        var sut = new Client("http://localhost:1/storage/v1");
        var thrown = await FluentActions.Awaiting(() => sut.Vectors.GetVectorBucketAsync(name!))
            .Should().ThrowAsync<ArgumentException>("a blank bucket name can never address a bucket and must not reach the service");
        using (new AssertionScope())
        {
            thrown.Which.ParamName.Should().Be("vectorBucketName");
            thrown.Which.Message.Should().Contain("null, empty or whitespace", "the caller needs to know why the name was refused");
        }
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldNotSendARequest_GivenABlankBucketName()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        await FluentActions.Awaiting(() => sut.Vectors.GetVectorBucketAsync(" ")).Should().ThrowAsync<ArgumentException>();
        handler.ReceivedCalls().Should().BeEmpty("validation happens before any I/O");
    }
}
