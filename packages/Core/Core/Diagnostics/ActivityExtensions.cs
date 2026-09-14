using System;
using System.Diagnostics;

namespace Supabase.Core.Diagnostics;

/// <summary>
/// Null-safe helpers for tagging activities with OpenTelemetry HTTP semantic conventions.
///
/// All members accept a null <see cref="Activity"/> (the result of
/// <see cref="ActivitySource.StartActivity(string, ActivityKind)"/> when nothing is listening)
/// and no-op, so call sites need no listener checks. URLs are recorded through
/// <see cref="UrlSanitizer"/> only — never record a raw URL, query string, token, or other
/// credential as a tag value.
/// </summary>
public static class ActivityExtensions
{
    /// <param name="activity">The activity to tag, or null when nothing is listening.</param>
    extension(Activity? activity)
    {
        /// <summary>
        /// Tags an outgoing HTTP request following OpenTelemetry HTTP client conventions.
        /// The URL is sanitized to scheme/host/port/path; the query string is never recorded.
        /// </summary>
        /// <param name="method">The HTTP method, e.g. <c>POST</c>.</param>
        /// <param name="uri">The request URI, sanitized before tagging.</param>
        public Activity? SetHttpRequestTags(string method, Uri uri)
        {
            if (activity == null)
                return null;

            activity.SetTag("http.request.method", method);
            activity.SetTag("server.address", uri.Host);
            if (!uri.IsDefaultPort)
                activity.SetTag("server.port", uri.Port);

            activity.SetTag("url.full", UrlSanitizer.Sanitize(uri));
            return activity;
        }

        /// <summary>
        /// Tags the response status code, marking the activity as failed for 4xx/5xx responses.
        /// </summary>
        /// <param name="statusCode">The HTTP response status code.</param>
        public Activity? SetHttpResponseTags(int statusCode)
        {
            if (activity == null)
                return null;

            activity.SetTag("http.response.status_code", statusCode);
            return statusCode >= 400 ? activity.SetErrorTags(statusCode) : activity;
        }

        /// <summary>
        /// Marks the activity as failed with the exception type as <c>error.type</c>.
        /// </summary>
        /// <param name="exception">The exception whose type and message describe the failure.</param>
        public Activity? SetFailure(Exception exception)
        {
            if (activity == null)
                return null;

            activity.SetTag("error.type", exception.GetType().FullName);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            return activity;
        }


        private Activity? SetErrorTags(int statusCode)
        {
            activity!.SetTag("error.type", statusCode.ToString());
            activity.SetStatus(ActivityStatusCode.Error);
            return activity;
        }
    }

}
