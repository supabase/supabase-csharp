#region

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using Supabase.Core.Diagnostics;

#endregion

namespace Supabase.Gotrue;

/// <summary>
///     Diagnostics for the GoTrue client, exposed through <see cref="System.Diagnostics" /> so
///     consumers can subscribe with the OpenTelemetry SDK using <see cref="GotrueDiagnostics.SourceName" />
///     (<c>AddSource(...)</c> / <c>AddMeter(...)</c>). Emission is zero-cost when nothing is listening.
///     Telemetry must never carry secrets: URLs are recorded without their query string (GoTrue
///     puts grant types, tokens and API keys there) and no tag may contain a JWT, refresh token,
///     or other credential or PII.
/// </summary>
internal static class GotrueInstrumentation
{

    internal static readonly ActivitySource Source =
        Instrumentation.CreateActivitySource(typeof(GotrueInstrumentation).Assembly, GotrueDiagnostics.SourceName);

    private static readonly Meter Meter =
        Instrumentation.CreateMeter(typeof(GotrueInstrumentation).Assembly, GotrueDiagnostics.SourceName);

    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "supabase.gotrue.http.request.duration", "s", "Duration of HTTP requests sent by the GoTrue client.");

    /// <summary>
    ///     Starts the client span for an outgoing HTTP request, tagged per OpenTelemetry HTTP
    ///     conventions with the sanitized (query-less) URL. Returns null when nothing is listening.
    /// </summary>
    internal static Activity? StartHttpActivity(HttpMethod method, Uri uri) =>
        Source.StartActivity($"{method.Method} {uri.AbsolutePath}", ActivityKind.Client)
            .SetHttpRequestTags(method.Method, uri);

    /// <summary>
    ///     Records the request duration histogram for an HTTP request outcome.
    /// </summary>
    internal static void RecordRequest(HttpMethod method, Uri uri, int? statusCode, string? errorType, long startTimestamp)
    {
        if (!RequestDuration.Enabled)
            return;

        var tags = new TagList
        {
            { "http.request.method", method.Method },
            { "server.address", uri.Host },
            { "url.path", uri.AbsolutePath }
        };

        if (statusCode.HasValue)
            tags.Add("http.response.status_code", statusCode.Value);

        if (errorType != null)
            tags.Add("error.type", errorType);

        RequestDuration.Record(GetElapsedSeconds(startTimestamp), tags);
    }
    private static double GetElapsedSeconds(long startTimestamp) => (Stopwatch.GetTimestamp() - startTimestamp) / (double) Stopwatch.Frequency;

    /// <summary>Names of the domain spans emitted for the public GoTrue operations.</summary>
    internal static class Spans
    {
        internal const string SignUp = "gotrue.sign_up";
        internal const string SendMagicLink = "gotrue.send_magic_link";
        internal const string SignInWithIdToken = "gotrue.sign_in.id_token";
        internal const string LinkIdentityWithIdToken = "gotrue.link_identity.id_token";
        internal const string GetUserIdentities = "gotrue.get_user_identities";
        internal const string SignInWithOtp = "gotrue.sign_in.otp";
        internal const string SignIn = "gotrue.sign_in";
        internal const string SignInAnonymously = "gotrue.sign_in.anonymous";
        internal const string VerifyOtp = "gotrue.verify_otp";
        internal const string VerifyTokenHash = "gotrue.verify_token_hash";
        internal const string SignOut = "gotrue.sign_out";
        internal const string UpdateUser = "gotrue.update_user";
        internal const string RefreshSession = "gotrue.refresh_session";
        internal const string SetSession = "gotrue.set_session";
        internal const string RetrieveSession = "gotrue.retrieve_session";
        internal const string ExchangeCode = "gotrue.exchange_code";
        internal const string RefreshToken = "gotrue.refresh_token";
    }

    /// <summary>Keys of the tags set on the domain spans.</summary>
    internal static class Tags
    {
        internal const string SignUpType = "gotrue.sign_up.type";
        internal const string Provider = "gotrue.provider";
        internal const string OtpChannel = "gotrue.otp.channel";
        internal const string SignInType = "gotrue.sign_in.type";
        internal const string SignOutScope = "gotrue.sign_out.scope";
    }

    /// <summary>Values for the <see cref="Tags.OtpChannel" /> tag.</summary>
    internal static class Channels
    {
        internal const string Email = "email";
        internal const string Phone = "phone";
    }
}