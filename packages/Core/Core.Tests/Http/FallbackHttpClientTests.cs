using System;
using System.Net.Http;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Core.Http;

namespace Core.Tests.Http;

/// <summary>
/// Covers <see cref="FallbackHttpClient"/>: the shared client a request falls back to when none was injected. It is
/// never built while a client is injected, and built once on first use otherwise.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class FallbackHttpClientTests
{
    [TestMethod]
    public void Or_ShouldNeverCreateTheFallback_GivenAnInjectedClient()
    {
        var create = Substitute.For<Func<HttpClient>>();
        using var injected = new HttpClient();
        new FallbackHttpClient(create).Or(injected).Should().BeSameAs(injected,
            "a host that injects its client (e.g. Unity WebGL, without System.Net networking) must never construct a default HttpClient/handler");
        create.DidNotReceive().Invoke();
    }

    [TestMethod]
    public void Or_ShouldCreateTheFallbackOnceAndReuseIt_GivenNoInjectedClient()
    {
        using var fallback = new HttpClient();
        var create = Substitute.For<Func<HttpClient>>();
        create().Returns(fallback);
        create.ClearReceivedCalls();
        var sut = new FallbackHttpClient(create);
        sut.Or(null).Should().BeSameAs(sut.Or(null)).And.BeSameAs(fallback, "the fallback is one shared client, as the eager static was");
        create.Received(1).Invoke();
    }

    [TestMethod]
    public void Value_ShouldCreateTheFallbackOnceAndReuseIt()
    {
        using var fallback = new HttpClient();
        var create = Substitute.For<Func<HttpClient>>();
        create().Returns(fallback);
        create.ClearReceivedCalls();
        var sut = new FallbackHttpClient(create);
        sut.Value.Should().BeSameAs(sut.Value).And.BeSameAs(fallback, "a sender with no injection seam still shares one client");
        create.Received(1).Invoke();
    }

    [TestMethod]
    public void Value_ShouldUseTheDefaultFactoryTimeout_GivenNoFactory() =>
        new FallbackHttpClient().Value.Timeout.Should().Be(TimeSpan.FromSeconds(100), "the default fallback matches the BCL default client it replaces");
}
