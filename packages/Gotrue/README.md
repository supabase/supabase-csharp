# Supabase.Gotrue

[![Build and Test](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase.Gotrue)](https://www.nuget.org/packages/Supabase.Gotrue/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)

A C# client for [Supabase Auth](https://supabase.com/docs/guides/auth) (GoTrue) — email/password,
OAuth providers, SSO, magic links, and user management.

Part of the [Supabase C# SDK](https://github.com/supabase/supabase-csharp). Most projects
use it through the [`Supabase`](../Supabase/README.md) meta-package (`supabase.Auth`); reference this
package directly to use Auth on its own. The client is written to be platform-agnostic and works on
.NET, Xamarin, MAUI, and Unity — see the [Unity session-persistence example](https://gist.github.com/wiverson/fbb07498743dff19b72c9c58599931e9).

## Installation

```sh
dotnet add package Supabase.Gotrue
```

Targets .NET Standard 2.1.

## Getting started

Against the Supabase hosted service, point the client at your project's auth URL and pass your
`apikey` header:

```csharp
using Supabase.Gotrue;

var client = new Client(new ClientOptions
{
    Url = "https://PROJECT_ID.supabase.co/auth/v1",
    Headers = new Dictionary<string, string> { { "apikey", SUPABASE_PUBLIC_KEY } }
});

var session = await client.SignUp("new-user@example.com", "password");
```

There is also a `StatelessClient` for one-off API calls that carry their options per request rather
than holding a session:

```csharp
var options = new StatelessClientOptions { Url = "https://example.com/auth/v1" };
await new StatelessClient().SignUp("new-user@example.com", "password", options);
```

## Sessions: persist, retrieve, destroy

The client is agnostic about where sessions are stored. `ClientOptions` exposes callbacks for saving,
loading, and destroying a session; when they are set together with `AutoRefreshToken`, the client
restores and refreshes an existing session as it initializes.

```csharp
async void Initialize()
{
    var persistence = new GotrueSessionPersistence(SaveSession, LoadSession, DestroySession);
    var client = new Client(new ClientOptions
    {
        Url = GOTRUE_URL,
        AllowUnconfirmedUserSessions = true,
        SessionPersistence = persistence
    });

    // Listen to token-refresh problems and auth-state changes.
    client.AddDebugListener(LogDebug);
    client.AddStateChangedListener(AuthStateListener);

    // Restore a persisted session and refresh it.
    client.LoadSession();
    await client.RetrieveSessionAsync();
}

// Example: persist the session to the user's cache folder.
bool SaveSession(Session session)
{
    var path = Path.Join(FileSystem.CacheDirectory, ".gotrue.cache");
    File.WriteAllText(path, JsonSerializer.Serialize(session));
    return true;
}
```

## JWT claims

`GetClaimsAsync` verifies an access token and returns its claims. It uses the current session's token
unless you provide one. RS256 and ES256 tokens can be verified locally using cached public keys;
otherwise, verification requires a request to the Auth server.

```csharp
var result = await client.GetClaimsAsync();
var userId = result.Claims.Sub;

// Verify an explicitly supplied access token.
var incoming = await client.GetClaimsAsync(accessToken);
```

## OAuth (PKCE flow)

For third-party OAuth the PKCE flow is preferred. Configure a callback URL in the Supabase dashboard,
generate a sign-in request, store the `PKCEVerifier`, and exchange the returned code for a session in
your callback:

```csharp
var state = await client.SignIn(Constants.Provider.Github, new SignInOptions
{
    FlowType = Constants.OAuthFlowType.PKCE,
    RedirectTo = "http://localhost:3000/oauth/callback"
});

// Send the user to state.Uri, and stash state.PKCEVerifier for the callback.

// In the callback (URL is http://REDIRECT_TO_URL?code=CODE):
var session = await client.ExchangeCodeForSession(state.PKCEVerifier, code);
```

## Single Sign-On (SSO)

SSO lets an enterprise account sign in across many apps (Okta, Microsoft Entra, Google Workspace, …).
Add an SSO provider to your project via the Supabase CLI first — see
[the SSO guide](https://supabase.com/docs/guides/auth/enterprise-sso/auth-sso-saml). The flow mirrors
OAuth; the GoTrue server handles the SAML exchange and appends session info to your redirect URL:

```csharp
var ssoResponse = await client.SignInWithSSO("supabase.io", new SignInWithSSOOptions
{
    RedirectTo = "https://localhost:3000/welcome"
});

// Send the user to ssoResponse.Uri. On return (URL carries the session), exchange it:
var session = await client.GetSessionFromUrl(url);
```

## Token refresh

GoTrue servers typically expire the access token after an hour, and the client refreshes it in the
background when ~20% of that time remains. If your server issues long-lived tokens (up to a week), you
can cap how long the client waits between refreshes with `MaximumRefreshWaitTime` (seconds, default
`14400` — four hours). With a one-hour expiry nothing changes; with a week-long expiry, a user who
opens the app at least once a week never has to re-authenticate.

## Offline support

The client supports online/offline usage through an `Online` flag, which you can drive from device
network status:

```csharp
var client = new Client(new ClientOptions { AllowUnconfirmedUserSessions = true });

var status = new NetworkStatus { Client = client };
await status.StartAsync();
```

This applies only to the stateful `Client` and its managed sessions — admin JWT methods and the
`StatelessClient` are unaffected. By default this changes nothing for existing code.

## Observability (OpenTelemetry)

The client emits traces and metrics through `System.Diagnostics`, so you can wire them into
OpenTelemetry (or any `ActivityListener` / `MeterListener`) without taking a dependency on the
OpenTelemetry packages. Emission is zero-cost while nothing is listening, so it is always on and stays
silent until you subscribe.

Register the client's `ActivitySource` and `Meter` by name. Use the `GotrueDiagnostics.SourceName`
constant rather than hardcoding the string, so a typo becomes a compile error instead of a silent
no-op:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Supabase.Gotrue;

// Requires OpenTelemetry.Extensions.Hosting and an exporter package (e.g. OTLP) in your app.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(GotrueDiagnostics.SourceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(GotrueDiagnostics.SourceName)
        .AddOtlpExporter());
```

Once subscribed you get:

- A span per public operation (`gotrue.sign_in`, `gotrue.refresh_token`, …) with a child client span
  for the underlying HTTP call following OpenTelemetry HTTP conventions (method, status code, and a
  sanitized URL — the query string, which carries grant types and API keys, is never recorded).
- A `supabase.gotrue.http.request.duration` histogram (seconds), tagged with method, host, path, and
  status code.

If you are not using the OpenTelemetry SDK, a raw listener works too:

```csharp
using System.Diagnostics;
using Supabase.Gotrue;

using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == GotrueDiagnostics.SourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity => Console.WriteLine($"{activity.OperationName} {activity.Duration.TotalMilliseconds}ms {activity.Status}")
};
ActivitySource.AddActivityListener(listener);
```

> The older `AddDebugListener`-based debug surface remains for logging, but OpenTelemetry is the
> recommended path for tracing and metrics.

## Troubleshooting

**I created a user but signing in throws an exception.** Provided the credentials are correct, make
sure the user has confirmed their email. Handling email confirmation in a desktop or mobile app means
registering platform URL handlers, which can be fiddly
([Windows](https://learn.microsoft.com/en-us/windows/win32/search/-search-3x-wds-ph-install-registration),
[Apple](https://developer.apple.com/documentation/xcode/defining-a-custom-url-scheme-for-your-app),
[Android](https://developer.android.com/training/app-links)). Many find it simpler to deploy a small
web page to handle confirmation, then have the app inspect the returned user to see if it is confirmed.

## Contributing

Contributions are welcome. See the [repository root](https://github.com/supabase/supabase-csharp)
for how to build and test the SDK.

## License

[MIT](../../LICENSE)
