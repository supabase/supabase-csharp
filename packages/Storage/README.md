# Supabase.Storage

[![Build and Test](https://github.com/supabase-community/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase-community/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase.Storage)](https://www.nuget.org/packages/Supabase.Storage/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)

A C# client for [Supabase Storage](https://supabase.com/docs/guides/storage) — upload, download, and
serve files from buckets, with signed and public URLs.

Part of the [Supabase C# SDK](https://github.com/supabase-community/supabase-csharp). Most projects
use it through the [`Supabase`](../Supabase/README.md) meta-package (`supabase.Storage`); reference
this package directly when you only need storage.

## Installation

```sh
dotnet add package Supabase.Storage
```

Targets .NET Standard 2.0.

## Getting started

Create a client pointed at your project's Storage URL, passing your key as a header:

```csharp
using Supabase.Storage;

var client = new Client("https://PROJECT_ID.supabase.co/storage/v1", new Dictionary<string, string>
{
    { "apikey", SUPABASE_KEY },
    { "Authorization", $"Bearer {SUPABASE_KEY}" }
});
```

### Buckets

```csharp
await client.CreateBucket("avatars");

var bucket = await client.GetBucket("avatars");
var all = await client.ListBuckets();

await client.EmptyBucket("avatars");
await client.DeleteBucket("avatars");
```

### Files

Work with a bucket's files through `From`:

```csharp
var bucket = client.From("avatars");

// Upload from a local path or from bytes.
await bucket.Upload("./local.png", "me.png");
await bucket.Upload(bytes, "me.png");

// List, move, copy, remove.
var files = await bucket.List();
await bucket.Move("me.png", "old/me.png");
await bucket.Copy("old/me.png", "backup/me.png");
await bucket.Remove("old/me.png");

// Download to a local path (returns the path) or into memory (returns bytes).
await bucket.Download("me.png", "./downloaded.png");
byte[] data = await bucket.Download("me.png");
```

### URLs

```csharp
// Public bucket: build a URL directly (pass null for no image transform).
var publicUrl = bucket.GetPublicUrl("me.png", null);

// Private bucket: sign a URL that expires (seconds).
var signedUrl = await bucket.CreateSignedUrl("me.png", 3600);
```

## Error handling

Storage failures throw a `SupabaseStorageException`. Use `Code` for the exact service error (for
example, `NoSuchKey`) and `Reason` for the broader classification. `Code` is `null` when the response
does not include one. See the [Storage error code reference](https://supabase.com/docs/guides/storage/debugging/error-codes).

## Observability (OpenTelemetry)

The client emits traces and metrics through `System.Diagnostics`, so you can wire them into
OpenTelemetry (or any `ActivityListener` / `MeterListener`) without taking a dependency on the
OpenTelemetry packages. Emission is zero-cost while nothing is listening, so it is always on and stays
silent until you subscribe.

Register the client's `ActivitySource` and `Meter` by name. Use the `StorageDiagnostics.SourceName`
constant rather than hardcoding the string, so a typo becomes a compile error instead of a silent
no-op:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Supabase.Storage;

// Requires OpenTelemetry.Extensions.Hosting and an exporter package (e.g. OTLP) in your app.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(StorageDiagnostics.SourceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(StorageDiagnostics.SourceName)
        .AddOtlpExporter());
```

Once subscribed you get:

- A client span per request, named `{METHOD} {path}` and following OpenTelemetry HTTP conventions
  (method, status code, and a sanitized URL). The query string is **never** recorded — Storage signed
  URLs carry a `token` there. Upload and download spans additionally carry a
  `storage.transfer.direction` tag (`upload` / `download`). The resumable (TUS) upload is reported as
  a single operation span covering the whole transfer, rather than one span per underlying chunk
  request.
- `supabase.storage.http.request.duration` (seconds) for control-plane requests.
- `supabase.storage.transfer.duration` (seconds) and `supabase.storage.transfer.size` (bytes) for
  uploads and downloads, tagged by direction — because a duration alone does not describe a file
  transfer.

If you are not using the OpenTelemetry SDK, a raw listener works too:

```csharp
using System.Diagnostics;
using Supabase.Storage;

using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == StorageDiagnostics.SourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity => Console.WriteLine($"{activity.OperationName} {activity.Duration.TotalMilliseconds}ms {activity.Status}")
};
ActivitySource.AddActivityListener(listener);
```

## Contributing

Contributions are welcome. See the [repository root](https://github.com/supabase-community/supabase-csharp)
for how to build and test the SDK.

## License

[MIT](../../LICENSE)
