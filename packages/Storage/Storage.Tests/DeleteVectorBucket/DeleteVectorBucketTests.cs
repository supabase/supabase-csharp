using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;

namespace Storage.Tests.DeleteVectorBucket;

/// <summary>
/// Unit tests for the checks <c>Vectors.DeleteVectorBucketAsync</c> makes before it touches the wire.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeleteVectorBucketTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task DeleteVectorBucketAsync_ShouldThrowArgumentException_GivenABlankName(string? name)
    {
        var sut = new Client("http://localhost:1/storage/v1");
        var thrown = await FluentActions.Awaiting(() => sut.Vectors.DeleteVectorBucketAsync(name!))
            .Should().ThrowAsync<ArgumentException>("a blank name can never address a bucket and must not reach the service");
        using (new AssertionScope())
        {
            thrown.Which.ParamName.Should().Be("vectorBucketName");
            thrown.Which.Message.Should().Contain("null, empty or whitespace", "the caller needs to know why the name was refused");
        }
    }

    [TestMethod]
    public async Task DeleteVectorBucketAsync_ShouldNotSendARequest_GivenABlankName()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        await FluentActions.Awaiting(() => sut.Vectors.DeleteVectorBucketAsync(" ")).Should().ThrowAsync<ArgumentException>();
        handler.ReceivedCalls().Should().BeEmpty("validation happens before any I/O");
    }
}
