using System;
using System.Net.Http;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Postgrest;

namespace Postgrest.Tests.Requests;

/// <summary>
///     The shared client a request falls back to when none was injected: it is never built while a client is
///     injected, and built once on first use otherwise.
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
}
