using System;
using System.Net;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;

namespace Core.Tests.Http;

/// <summary>Covers <see cref="DefaultHttpClientFactory"/>: the fallback client built when a consumer hasn't injected one.</summary>
[TestClass]
[TestCategory("Unit")]
public class DefaultHttpClientFactoryTests
{
    [TestMethod]
    public void Create_ShouldReturnBclDefaultTimeout_GivenNoTimeoutSpecified() =>
        DefaultHttpClientFactory.Create().Timeout.Should().Be(TimeSpan.FromSeconds(100));

    [TestMethod]
    public void Create_ShouldApplyGivenTimeout() =>
        DefaultHttpClientFactory.Create(timeout: TimeSpan.FromSeconds(30)).Timeout.Should().Be(TimeSpan.FromSeconds(30));

    [TestMethod]
    public void Create_ShouldNotThrow_GivenAProxy() =>
        FluentActions.Invoking(() => DefaultHttpClientFactory.Create(proxy: new WebProxy("http://localhost:8888"))).Should().NotThrow();
}
