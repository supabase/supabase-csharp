#region

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using FluentAssertions;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.Server;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     A hermetic stand-in for the GoTrue HTTP API: Contract-tier tests point a real <c>Client</c> at this
///     WireMock server, stub the endpoints they exercise, and assert the exact request the SDK put on the wire.
/// </summary>
internal sealed class MockGotrueServer : IDisposable
{
    internal const string ApiKey = "test-api-key";

    private readonly WireMockServer server = WireMockServer.Start();

    internal string Url => this.server.Url!;

    public void Dispose() => this.server.Stop();

    internal IRespondWithAProvider Given(IRequestBuilder requestBuilder) => this.server.Given(requestBuilder);

    internal void Reset() => this.server.ResetMappings();

    internal ReceivedRequest VerifySingleReceivedRequest()
    {
        var entry = this.server.LogEntries.Should().ContainSingle("the SDK should emit exactly one request").Which;
        return new ReceivedRequest(entry.RequestMessage!);
    }
}

/// <summary>
///     Fluent assertions over a request the SDK sent to the <see cref="MockGotrueServer" />.
/// </summary>
internal sealed class ReceivedRequest
{
    private readonly IRequestMessage request;

    internal ReceivedRequest(IRequestMessage request) => this.request = request;

    /// <summary>
    ///     The exact request body the SDK put on the wire, verbatim — the bytes a wire-shape snapshot pins.
    /// </summary>
    internal string RawBody
    {
        get
        {
            this.request.Body.Should().NotBeNull("the request should have a body");
            return this.request.Body!;
        }
    }

    internal ReceivedRequest WithPath(string path)
    {
        this.request.Path.Should().Be(path);
        return this;
    }

    internal ReceivedRequest WithMethod(HttpMethod method)
    {
        this.request.Method.Should().Be(method.ToString());
        return this;
    }

    internal ReceivedRequest WithQueryParam(string name, string expected)
    {
        this.request.Query.Should().ContainKey(name).WhoseValue.Single().Should().Be(expected);
        return this;
    }

    internal ReceivedRequest WithHeader(string name, string expected)
    {
        this.request.Headers.Should().ContainKey(name).WhoseValue.Single().Should().Be(expected);
        return this;
    }

    internal ReceivedRequest WithoutHeader(string name)
    {
        this.request.Headers!.Keys.Should().NotContain(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase),
            $"'{name}' should not have been sent");
        return this;
    }

    internal ReceivedRequest WithJsonContentType()
    {
        this.request.Headers.Should().ContainKey("Content-Type").WhoseValue.Single().Should().StartWith("application/json");
        return this;
    }

    internal ReceivedRequest WithExactJsonBody(string field, string expected)
    {
        this.request.Body.Should().NotBeNull("the request should have a body");
        var body = JsonNode.Parse(this.request.Body!)!.AsObject();
        body[field]?.GetValue<string>().Should().Be(expected);
        return this;
    }

    internal ReceivedRequest WithBooleanJsonBody(string field, bool expected)
    {
        this.request.Body.Should().NotBeNull("the request should have a body");
        var body = JsonNode.Parse(this.request.Body!)!.AsObject();
        body[field]?.GetValue<bool>().Should().Be(expected);
        return this;
    }

    internal ReceivedRequest WithNestedJsonBody(string parent, string field, string expected)
    {
        this.request.Body.Should().NotBeNull("the request should have a body");
        var body = JsonNode.Parse(this.request.Body!)!.AsObject();
        body[parent]?[field]?.GetValue<string>().Should().Be(expected);
        return this;
    }

    internal ReceivedRequest WithoutJsonBodyField(string field)
    {
        this.request.Body.Should().NotBeNull("the request should have a body");
        var body = JsonNode.Parse(this.request.Body!)!.AsObject();
        body.ContainsKey(field).Should().BeFalse($"'{field}' should be omitted when not supplied");
        return this;
    }

    internal string? ReadJsonBodyField(string field)
    {
        this.request.Body.Should().NotBeNull("the request should have a body");
        var body = JsonNode.Parse(this.request.Body!)!.AsObject();
        return body[field]?.GetValue<string>();
    }
}
