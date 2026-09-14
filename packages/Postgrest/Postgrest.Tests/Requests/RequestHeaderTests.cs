using System.Collections.Generic;
using System.Net.Http;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Postgrest;

namespace Postgrest.Tests.Requests;

/// <summary>
///     <see cref="Helpers.PrepareRequestHeaders" /> assembles the headers every request carries: caller
///     headers pass through, the configured schema becomes a profile header (which one depends on the verb),
///     a requested row range becomes <c>Range</c> headers, and a default <c>X-Client-Info</c> identifies the SDK.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class RequestHeaderTests
{
    [TestMethod]
    public void PrepareRequestHeaders_ShouldPassThroughCallerHeaders() => Helpers.PrepareRequestHeaders(HttpMethod.Get, new Dictionary<string, string> { { "Authorization", "Bearer token" } })["Authorization"].Should().Be("Bearer token");

    [TestMethod]
    public void PrepareRequestHeaders_ShouldUseAcceptProfile_GivenSchemaOnAGetRequest()
    {
        var headers = Helpers.PrepareRequestHeaders(HttpMethod.Get, options: new ClientOptions { Schema = "personal" });
        headers.Should().ContainKey("Accept-Profile").WhoseValue.Should().Be("personal");
        headers.Should().NotContainKey("Content-Profile");
    }

    [TestMethod]
    public void PrepareRequestHeaders_ShouldUseContentProfile_GivenSchemaOnAWritingRequest()
    {
        var headers = Helpers.PrepareRequestHeaders(HttpMethod.Post, options: new ClientOptions { Schema = "personal" });
        headers.Should().ContainKey("Content-Profile").WhoseValue.Should().Be("personal");
        headers.Should().NotContainKey("Accept-Profile");
    }

    [TestMethod]
    public void PrepareRequestHeaders_ShouldEmitRangeHeaders_GivenARowRange()
    {
        var headers = Helpers.PrepareRequestHeaders(HttpMethod.Get, rangeFrom: 1, rangeTo: 3);
        headers["Range-Unit"].Should().Be("items");
        headers["Range"].Should().Be("1-3");
    }

    [TestMethod]
    public void PrepareRequestHeaders_ShouldLeaveOpenEndedRange_GivenNoUpperBound() => Helpers.PrepareRequestHeaders(HttpMethod.Get, rangeFrom: 2)["Range"].Should().Be("2-");

    [TestMethod]
    public void PrepareRequestHeaders_ShouldIdentifyTheSdkViaXClientInfo() =>
        Helpers.PrepareRequestHeaders(HttpMethod.Get).Should().ContainKey("X-Client-Info").WhoseValue.Should().Be(Supabase.Core.Util.GetAssemblyVersion(typeof(Client)));
}
