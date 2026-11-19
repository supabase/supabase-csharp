using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Supabase.Realtime.Socket;

/// <summary>
/// Converts between the message the client works with, the JSON object of protocol 1.0.0, and the frames of
/// protocol 2.0.0: a JSON array in a text frame, and the user broadcast layouts in a binary frame.
/// </summary>
internal static class WireFrame
{
    private const byte UserBroadcastPushType = 3;
    private const byte UserBroadcastType = 4;
    private const byte NoMetadata = 0;
    private const byte BinaryEncoding = 0;
    private const int UserBroadcastHeaderSize = 5;
    private static readonly string[] Fields = { "join_ref", "ref", "topic", "event", "payload" };

    /// <summary>
    /// Whether <paramref name="field"/> fits the one byte a binary frame has for its size.
    /// </summary>
    internal static bool Fits(string field) => Encoding.UTF8.GetByteCount(field) <= byte.MaxValue;

    /// <summary>
    /// Turns a 1.0.0 message into the 2.0.0 text frame.
    /// </summary>
    internal static string ToArray(string message)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;
        return $"[{string.Join(",", Fields.Select(field => root.TryGetProperty(field, out var value) ? value.GetRawText() : "null"))}]";
    }

    /// <summary>
    /// Turns a 2.0.0 text frame into the 1.0.0 message.
    /// </summary>
    internal static string ToObject(string frame)
    {
        using var document = JsonDocument.Parse(frame);
        return $"{{{string.Join(",", document.RootElement.EnumerateArray().Zip(Fields, (value, field) => $"\"{field}\":{value.GetRawText()}"))}}}";
    }

    /// <summary>
    /// Turns a binary user broadcast frame into the 1.0.0 broadcast message. A payload that is not JSON is left out
    /// of the message and returned in <paramref name="binaryPayload"/>.
    /// </summary>
    internal static string ToObject(byte[] frame, out byte[]? binaryPayload)
    {
        if (frame[0] != UserBroadcastType)
            throw new NotSupportedException($"Binary frame type {frame[0]} is not a user broadcast.");

        var offset = UserBroadcastHeaderSize;
        string Next(int size)
        {
            var value = Encoding.UTF8.GetString(frame, offset, size);
            offset += size;
            return value;
        }

        var topic = JsonSerializer.Serialize(Next(frame[1]));
        var userEvent = JsonSerializer.Serialize(Next(frame[2]));
        var metadata = Next(frame[3]);
        var meta = metadata.Length > 0 ? $",\"meta\":{metadata}" : string.Empty;
        binaryPayload = frame[4] == BinaryEncoding ? frame.AsSpan(offset).ToArray() : null;
        var payload = binaryPayload == null ? $",\"payload\":{Next(frame.Length - offset)}" : string.Empty;
        return $"{{\"topic\":{topic},\"event\":\"broadcast\",\"payload\":{{\"type\":\"broadcast\",\"event\":{userEvent}{meta}{payload}}}}}";
    }

    /// <summary>
    /// Builds the binary frame that pushes a user broadcast whose payload is not JSON.
    /// </summary>
    internal static byte[] UserBroadcastPush(string joinRef, string reference, string topic, string userEvent, byte[] payload)
    {
        var fields = new[] { joinRef, reference, topic, userEvent }.Select(Encoding.UTF8.GetBytes).ToArray();
        using var frame = new MemoryStream();
        frame.WriteByte(UserBroadcastPushType);
        foreach (var field in fields)
            frame.WriteByte((byte) field.Length);
        frame.WriteByte(NoMetadata);
        frame.WriteByte(BinaryEncoding);
        foreach (var field in fields)
            frame.Write(field, 0, field.Length);
        frame.Write(payload, 0, payload.Length);
        return frame.ToArray();
    }
}
