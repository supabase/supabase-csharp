# Supabase C# SDK

[![Build and Test](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase?label=Supabase)](https://www.nuget.org/packages/Supabase/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)

The official C# client for [Supabase](https://supabase.com). This is a monorepo: the top-level
`Supabase` client and every service package it builds on live here, under [`packages/`](./packages).

Use the `Supabase` meta-package for the full client, or reference an individual service package
(Auth, Postgrest, Storage, Realtime, Functions) on its own.

## Packages

| Package | NuGet | Source | Purpose |
| --- | --- | --- | --- |
| **Supabase** | [![NuGet](https://img.shields.io/nuget/vpre/Supabase)](https://www.nuget.org/packages/Supabase/) | [`packages/Supabase`](./packages/Supabase) | The unified client that wires the services below together. Start here. |
| **Supabase.Gotrue** | [![NuGet](https://img.shields.io/nuget/vpre/Supabase.Gotrue)](https://www.nuget.org/packages/Supabase.Gotrue/) | [`packages/Gotrue`](./packages/Gotrue) | Authentication — email/password, OAuth, SSO, magic links. |
| **Supabase.Postgrest** | [![NuGet](https://img.shields.io/nuget/vpre/Supabase.Postgrest)](https://www.nuget.org/packages/Supabase.Postgrest/) | [`packages/Postgrest`](./packages/Postgrest) | Query your database through the auto-generated REST API, with LINQ. |
| **Supabase.Storage** | [![NuGet](https://img.shields.io/nuget/vpre/Supabase.Storage)](https://www.nuget.org/packages/Supabase.Storage/) | [`packages/Storage`](./packages/Storage) | File storage — upload, download, signed URLs, buckets. |
| **Supabase.Realtime** | [![NuGet](https://img.shields.io/nuget/vpre/Supabase.Realtime)](https://www.nuget.org/packages/Supabase.Realtime/) | [`packages/Realtime`](./packages/Realtime) | Realtime — Postgres changes, Broadcast, and Presence over websockets. |
| **Supabase.Functions** | [![NuGet](https://img.shields.io/nuget/vpre/Supabase.Functions)](https://www.nuget.org/packages/Supabase.Functions/) | [`packages/Functions`](./packages/Functions) | Invoke Edge Functions. |
| **Supabase.Core** | [![NuGet](https://img.shields.io/nuget/vpre/Supabase.Core)](https://www.nuget.org/packages/Supabase.Core/) | [`packages/Core`](./packages/Core) | Shared primitives used by the packages above. Rarely referenced directly. |

Every package targets .NET Standard 2.1, so it runs on .NET Core 3.0+ / .NET 5+, recent MAUI, Xamarin, and Unity.
See the [wiki](https://github.com/supabase/supabase-csharp/wiki) for platform-specific guides
(Unity, desktop/mobile, server-side).

## Installation

```sh
dotnet add package Supabase
```

## Quickstart

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

// Auth
await supabase.Auth.SignIn("user@example.com", "password");

// Database
var movies = await supabase.From<Movie>().Get();

// Storage
await supabase.Storage.From("avatars").Download("me.png", null);
```

Each service is reached from the initialized client: `supabase.Auth`, `supabase.From<T>()` /
`supabase.Rpc(...)`, `supabase.Storage`, `supabase.Realtime`, and `supabase.Functions`. For using a
single service on its own, see that package's README.

> **A note on keys:** some APIs (user administration, bypassing RLS, etc.) require the `service_key`
> rather than the public/anon key. Never expose a `service_key` in client-side code, and use a
> separate client instance for service and user contexts.

## Observability (OpenTelemetry)

The clients emit traces and metrics through `System.Diagnostics`, so you can wire them into
OpenTelemetry (or any `ActivityListener` / `MeterListener`) without taking a dependency on the
OpenTelemetry packages. Emission is zero-cost while nothing is listening, so it is always on and
stays silent until you subscribe.

Register every instrumented source at once with `SupabaseDiagnostics.SourceNames` — each client
shares one name between its `ActivitySource` and its `Meter`, so the same list works for tracing and
metrics:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Supabase;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(SupabaseDiagnostics.SourceNames.ToArray())
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(SupabaseDiagnostics.SourceNames.ToArray())
        .AddOtlpExporter());
```

This covers the Auth, Postgrest, Functions, and Storage clients (Realtime is not yet instrumented).
URLs are recorded without their query string, and no token, credential, or payload is placed in a
tag. Each package's README documents the specific spans and metrics it produces.

## Documentation

- [Getting Started](https://github.com/supabase/supabase-csharp/wiki#getting-started)
- [Supabase C# reference](https://supabase.com/docs/reference/csharp/introduction)
- [Generated API docs](https://supabase-community.github.io/supabase-csharp/api/Supabase.Client.html)
- [Examples](https://github.com/supabase/supabase-csharp/wiki/Examples)
- [Troubleshooting](https://github.com/supabase/supabase-csharp/wiki/Troubleshooting) · [Discussions](https://github.com/supabase/supabase-csharp/discussions)

## Repository layout

```
packages/
  Supabase/    The unified client (NuGet: Supabase)
  Gotrue/      Auth        (NuGet: Supabase.Gotrue)
  Postgrest/   Database    (NuGet: Supabase.Postgrest)
  Storage/     Storage     (NuGet: Supabase.Storage)
  Realtime/    Realtime    (NuGet: Supabase.Realtime)
  Functions/   Functions   (NuGet: Supabase.Functions)
  Core/        Shared      (NuGet: Supabase.Core)
```

Each `packages/<Name>/` holds the library project and its test project. Package metadata common to
every project (license, authors, repository) lives in [`Directory.Build.props`](./Directory.Build.props);
dependency versions are managed centrally in [`Directory.Packages.props`](./Directory.Packages.props).

## Building & testing

The SDK builds with the .NET SDK version pinned in [`global.json`](./global.json).

```sh
dotnet restore
dotnet build Supabase.sln --configuration Release
dotnet test  Supabase.sln --configuration Release
```

Some test suites are end-to-end and need a local Supabase stack via the
[Supabase CLI](https://supabase.com/docs/guides/cli) (`supabase start`, which requires Docker). The
Realtime suite additionally expects `realtime-dev.localhost` to resolve locally — add a hosts entry:

```
127.0.0.1  realtime-dev.localhost
```

## Versioning & releases

Releases are automated with [release-please](https://github.com/googleapis/release-please). Commits
follow [Conventional Commits](https://www.conventionalcommits.org/); the changelog is generated in
[`CHANGELOG.md`](./CHANGELOG.md). For breaking changes and how to adapt to them across major
versions, see [the migration guides](./docs/migrations).

## Contributing

Contributions are welcome — please open an issue to discuss substantial changes, then submit a PR.

<a href="https://github.com/supabase/supabase-csharp/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=supabase-community/supabase-csharp" alt="Contributors" />
</a>

## License

[MIT](./LICENSE)
