using Supabase.Core.Attributes;

namespace Supabase.Realtime;

/// <summary>
/// The version of the Realtime protocol a <see cref="Client"/> speaks over its websocket.
/// </summary>
public enum ProtocolVersion
{
    /// <summary>
    /// Protocol 1.0.0: every message is a JSON object in a text frame.
    /// </summary>
    [MapTo("1.0.0")] V1,

    /// <summary>
    /// Protocol 2.0.0: messages are JSON arrays in text frames, and a broadcast can travel in a binary frame.
    /// </summary>
    [MapTo("2.0.0")] V2
}
