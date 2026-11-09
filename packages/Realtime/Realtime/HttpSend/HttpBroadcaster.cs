using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Realtime.Exceptions;

namespace Supabase.Realtime.HttpSend;

/// <summary>
/// Sends a channel's broadcasts through the Realtime REST API.
/// </summary>
internal sealed class HttpBroadcaster
{
    private const string TopicPrefix = "realtime:";

    private static readonly HttpClient Client = new();

    private readonly string endpoint;
    private readonly ClientOptions options;
    private readonly Func<Dictionary<string, string>> getHeaders;

    internal HttpBroadcaster(string realtimeUrl, ClientOptions options, Func<Dictionary<string, string>> getHeaders)
    {
        this.endpoint = $"http{realtimeUrl.Substring(2)}/api/broadcast";
        this.options = options;
        this.getHeaders = getHeaders;
    }

    internal async Task SendAsync(RealtimeChannel channel, string eventName, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, this.BuildUrl(channel, eventName))
        {
            Content = BuildContent(payload, channel.Options.SerializerSettings)
        };
        foreach (var header in this.BuildHeaders(channel.Options.RetrieveAccessToken()))
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        using var timeoutCts = new CancellationTokenSource(this.options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var response = await Client.SendAsync(request, linkedCts.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new RealtimeException(await response.Content.ReadAsStringAsync()) { StatusCode = (int) response.StatusCode };
    }

    private string BuildUrl(RealtimeChannel channel, string eventName)
    {
        var topic = channel.Topic.StartsWith(TopicPrefix, StringComparison.Ordinal)
            ? channel.Topic.Substring(TopicPrefix.Length)
            : channel.Topic;
        var url = $"{this.endpoint}/{Uri.EscapeDataString(topic)}/events/{Uri.EscapeDataString(eventName)}";
        return channel.Options.IsPrivate ? $"{url}?private=true" : url;
    }

    private static HttpContent BuildContent(object payload, JsonSerializerOptions serializerOptions) =>
        payload is byte[] bytes
            ? new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } }
            : new StringContent(JsonSerializer.Serialize(payload, serializerOptions), Encoding.UTF8, "application/json");

    private Dictionary<string, string> BuildHeaders(string? accessToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in this.getHeaders())
            headers[header.Key] = header.Value;
        if (this.options.Parameters.ApiKey != null)
            headers["apikey"] = this.options.Parameters.ApiKey;
        if (!string.IsNullOrEmpty(accessToken))
            headers["Authorization"] = $"Bearer {accessToken}";
        return headers;
    }
}
