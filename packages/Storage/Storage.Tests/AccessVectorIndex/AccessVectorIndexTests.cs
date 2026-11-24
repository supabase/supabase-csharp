using System;
using System.Net.Http;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Storage;

namespace Storage.Tests.AccessVectorIndex;

/// <summary>
/// Unit tests for <c>Vectors.From(bucket).Index(name)</c>: scoping is local, so only the name check and the absence of I/O are observable.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class AccessVectorIndexTests
{
    [TestMethod]
    public void Index_ShouldReturnAScope_GivenAName()
    {
        var sut = new Client("http://localhost:1/storage/v1");
        sut.Vectors.From("embeddings-prod").Index("docs-v1").Should().NotBeNull("the caller scopes later operations through it");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Index_ShouldThrowArgumentException_GivenABlankIndexName(string? name)
    {
        var sut = new Client("http://localhost:1/storage/v1");
        var thrown = FluentActions.Invoking(() => sut.Vectors.From("embeddings-prod").Index(name!))
            .Should().Throw<ArgumentException>("a blank index name can never address an index");
        using (new AssertionScope())
        {
            thrown.Which.ParamName.Should().Be("indexName");
            thrown.Which.Message.Should().Contain("null, empty or whitespace", "the caller needs to know why the name was refused");
        }
    }

    [TestMethod]
    public void Index_ShouldNotSendARequest()
    {
        var handler = Substitute.For<HttpMessageHandler>();
        var sut = new Client("http://localhost:1/storage/v1", new ClientOptions { HttpRequestClient = new HttpClient(handler) });
        sut.Vectors.From("embeddings-prod").Index("docs-v1");
        handler.ReceivedCalls().Should().BeEmpty("scoping to an index is local; only an operation reaches the service");
    }
}
