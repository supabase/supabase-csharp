using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using Supabase.Core.Http;
#pragma warning disable CS1591
namespace Supabase.Postgrest;


/// <summary>
/// Options that can be passed to the Client configuration
/// </summary>
public class ClientOptions
{
    public string Schema { get; set; } = "public";

    public readonly DateTimeStyles DateTimeStyles = DateTimeStyles.AdjustToUniversal;

    public const string DATE_TIME_FORMAT = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFK";

    /// <summary>
    /// When true, enum properties without their own `[JsonConverter]` attribute are serialized by
    /// name (e.g. "OffDisplay") instead of their underlying integer value. Enable this if your enum
    /// properties map to native PostgreSQL `enum` columns. Leave disabled (default) if any of your
    /// enum properties map to `integer`/`smallint` columns, since enabling this would send a string
    /// to a numeric column and PostgREST would reject the request.
    /// </summary>
    public bool SerializeEnumsAsStrings { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    public Dictionary<string, string> QueryParams { get; set; } = new Dictionary<string, string>();

    /// <summary>Retry policy applied to each request. Default (<see cref="RetryOptions.MaxRetries"/> 0) sends once, unretried.</summary>
    public RetryOptions Retry { get; set; } = new RetryOptions();

    /// <summary>An HttpClient to send requests through. When null, the client builds and owns its own.</summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>A proxy to route requests through. Only used when <see cref="HttpClient"/> is not supplied.</summary>
    public IWebProxy? Proxy { get; set; }
}
