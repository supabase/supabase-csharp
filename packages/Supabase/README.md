# Supabase

[![Build and Test](https://github.com/supabase-community/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase-community/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase)](https://www.nuget.org/packages/Supabase/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)

The unified C# client for [Supabase](https://supabase.com). One client composes Auth, Database,
Storage, Realtime, and Edge Functions, so you configure your project once and reach every service
from a single object.

Part of the [Supabase C# SDK](https://github.com/supabase-community/supabase-csharp). To use a single
service on its own, reference its package instead — see [Individual services](#individual-services).

## Installation

```sh
dotnet add package Supabase
```

Targets .NET Standard 2.1, so it runs on .NET Core / .NET 5+, Xamarin, MAUI, and Unity. See the
[wiki](https://github.com/supabase-community/supabase-csharp/wiki) for platform-specific guides.

## Getting started

Create a project in the [Supabase dashboard](https://app.supabase.com) and grab your project URL and
API key from **Settings → API** — a publishable key (`sb_publishable_…`) for client-side use, or the
legacy public (anon) key. Then initialize the client:

```csharp
using Supabase;

var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
var key = Environment.GetEnvironmentVariable("SUPABASE_KEY");

var options = new SupabaseOptions
{
    AutoRefreshToken = true,
    AutoConnectRealtime = true
};

var supabase = new Client(url, key, options);
await supabase.InitializeAsync();
```

`InitializeAsync()` wires up the child clients and, if a session has been persisted, restores and
refreshes it. Once it returns, reach each service from the client:

```csharp
// Auth
var session = await supabase.Auth.SignIn("user@example.com", "password");

// Database (models derive from BaseModel — see the Postgrest package)
var response = await supabase.From<Movie>().Get();
var movies = response.Models;

// Call a Postgres function
await supabase.Rpc("some_rpc", new { some_arg = "value" });

// Storage
await supabase.Storage.From("avatars").Upload("./local.png", "me.png");

// Realtime
var channel = supabase.Realtime.Channel("movies");
await channel.Subscribe();

// Edge Functions
await supabase.Functions.Invoke("hello-world");
```

## Configuration

Pass a `SupabaseOptions` to the constructor:

| Option | Default | Purpose |
| --- | --- | --- |
| `AutoRefreshToken` | `true` | Refresh the user's access token in the background before it expires. |
| `AutoConnectRealtime` | `false` | Connect the Realtime socket during `InitializeAsync()`. |
| `SessionHandler` | no-op | Persist, restore, and destroy the auth session (see below). |
| `Schema` | `"public"` | Postgres schema used by Database and Realtime. |
| `Headers` | empty | Extra headers forwarded to every child client. |
| `StorageClientOptions` | defaults | Options passed through to the Storage client. |

### Session persistence

By default the client does not persist the auth session, so the user is signed out when the process
ends. Provide a `SessionHandler` to save, load, and destroy the session — for example to a file or to
the platform's secure storage. See
[Authorization with Gotrue](https://github.com/supabase-community/supabase-csharp/wiki/Authorization-with-Gotrue#offline-support)
in the wiki for a worked example.

> **A note on keys:** some APIs (user administration, bypassing RLS, etc.) require an elevated key
> rather than the public/publishable one. Never expose an elevated key in client-side code, and use a
> separate client instance for service and user contexts.

### New API keys (publishable / secret)

Supabase is replacing the legacy JWT-based `anon` and `service_role` keys with opaque keys, deprecating
the legacy keys by the end of 2026. The SDK supports both:

| Key | Prefix | Use in |
| --- | --- | --- |
| Publishable | `sb_publishable_…` | Client-side apps, CLIs, scripts — replaces the `anon` key. |
| Secret | `sb_secret_…` | Server-side only (bypasses RLS) — replaces the `service_role` key. Never ship it in client code. |

Pass either as the `key` argument; no other code changes are required. Because the new keys are not
JWTs, they must travel on the `apikey` header only — the SDK detects the `sb_publishable_` / `sb_secret_`
prefixes and, on the Edge Functions path, stops sending the key as an `Authorization: Bearer` token when
there is no user session (the Functions gateway would otherwise reject it as an invalid JWT). Legacy JWT
keys are unaffected. See the Supabase guide on
[migrating to the new API keys](https://supabase.com/docs/guides/getting-started/migrating-to-new-api-keys).

## Observability (OpenTelemetry)

The clients emit traces and metrics through `System.Diagnostics`, so you can wire them into
OpenTelemetry (or any `ActivityListener` / `MeterListener`) without taking a dependency on the
OpenTelemetry packages. Emission is zero-cost while nothing is listening, so it is always on and stays
silent until you subscribe.

Register every instrumented source at once with `SupabaseDiagnostics.SourceNames` — each client
shares one name between its `ActivitySource` and its `Meter`, so the same list works for tracing and
metrics:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Supabase;

// Requires OpenTelemetry.Extensions.Hosting and an exporter package (e.g. OTLP) in your app.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(SupabaseDiagnostics.SourceNames.ToArray())
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(SupabaseDiagnostics.SourceNames.ToArray())
        .AddOtlpExporter());
```

This covers the Auth, Postgrest, Functions, and Storage clients. Realtime is **not** yet instrumented,
so no websocket telemetry is emitted. URLs are recorded without their query string, and no token,
credential, or payload is placed in a tag. Each service package's README documents the specific spans
and metrics it produces.

## Individual services

Each service is also published as a standalone package, useful when you only need one:

| Service | Package | Docs |
| --- | --- | --- |
| Auth | `Supabase.Gotrue` | [README](../Gotrue/README.md) |
| Database | `Supabase.Postgrest` | [README](../Postgrest/README.md) |
| Storage | `Supabase.Storage` | [README](../Storage/README.md) |
| Realtime | `Supabase.Realtime` | [README](../Realtime/README.md) |
| Functions | `Supabase.Functions` | [README](../Functions/README.md) |

## Documentation

- [Getting Started](https://github.com/supabase-community/supabase-csharp/wiki#getting-started)
- [Supabase C# reference](https://supabase.com/docs/reference/csharp/introduction)
- [Generated API docs](https://supabase-community.github.io/supabase-csharp/api/Supabase.Client.html)
- [Examples](https://github.com/supabase-community/supabase-csharp/wiki/Examples)
- [Troubleshooting](https://github.com/supabase-community/supabase-csharp/wiki/Troubleshooting) · [Discussions](https://github.com/supabase-community/supabase-csharp/discussions)

## Contributing

Contributions are welcome. See the [repository root](https://github.com/supabase-community/supabase-csharp)
for how to build and test the SDK.

## License

[MIT](../../LICENSE)
