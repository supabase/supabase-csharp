using System;
using Websocket.Client;

namespace Supabase.Realtime.Exceptions;

/// <summary>
/// An Exception thrown within <see cref="Realtime"/>
/// </summary>
public class RealtimeException : Exception
{
    /// <inheritdoc />
    public RealtimeException(string? message) : base(message)
    {
    }

    /// <inheritdoc />
    public RealtimeException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// A specific reason for this exception, as provided by this library.
    /// </summary>
    public FailureHint.Reason Reason { get; internal set; }

    /// <summary>
    /// The HTTP status code of a rejected <c>HttpSendAsync</c>.
    /// </summary>
    public int StatusCode { get; internal set; }

    /// <summary>
    /// Creates exception from a <see cref="DisconnectionInfo"/> instance.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public static RealtimeException FromDisconnectionInfo(DisconnectionInfo info) =>
        new(info.CloseStatusDescription, info.Exception)
        {
            Reason = FailureHint.Parse(info)
        };
}