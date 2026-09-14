# Supabase.Functions

[![Build and Test](https://github.com/supabase-community/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase-community/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase.Functions)](https://www.nuget.org/packages/Supabase.Functions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)

A C# client for invoking [Supabase Edge Functions](https://supabase.com/docs/guides/functions).

Part of the [Supabase C# SDK](https://github.com/supabase-community/supabase-csharp). Most projects
use it through the [`Supabase`](../Supabase/README.md) meta-package (`supabase.Functions`); reference
this package directly when you only need to invoke functions.

## Installation

```sh
dotnet add package Supabase.Functions
```

Targets .NET Standard 2.0.

## Getting started

Create a client pointed at your project's Functions URL, then invoke a function by name:

```csharp
using Supabase.Functions;

var client = new Client("https://PROJECT_ID.supabase.co/functions/v1");

// Invoke and read the raw string response.
var body = await client.Invoke("hello-world", token: SUPABASE_ANON_KEY);
```

`Invoke` needs a bearer token to authorize the request — your project's anon key, or a user's access
token. Through the `Supabase` meta-package the token is supplied for you.

### Deserializing the response

Use the generic overload to deserialize the JSON body into a type:

```csharp
var result = await client.Invoke<MyResponse>("hello-world", token: SUPABASE_ANON_KEY);
```

### Passing a body, headers, method, or region

`InvokeFunctionOptions` covers the common per-call settings:

```csharp
var options = new Client.InvokeFunctionOptions
{
    Body = new Dictionary<string, object> { { "name", "world" } },
    Headers = new Dictionary<string, string> { { "x-trace", "abc" } },
    HttpMethod = HttpMethod.Post,
    FunctionRegion = FunctionRegion.UsEast1
};

var result = await client.Invoke<MyResponse>("hello-world", token: SUPABASE_ANON_KEY, options);
```

### Streaming a response

`InvokeStream` returns successful responses after the headers arrive. `HttpTimeout` and request cancellation
cover the request and error-body reads. Cancel successful body reads separately and dispose the response when finished.

```csharp
using var response = await client.InvokeStream("chat", token: SUPABASE_ANON_KEY);
using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
string? line;
while ((line = await reader.ReadLineAsync()) != null)
    Console.WriteLine(line);
```

## Observability (OpenTelemetry)

The client emits traces and metrics through `System.Diagnostics`, so you can wire them into
OpenTelemetry (or any `ActivityListener` / `MeterListener`) without taking a dependency on the
OpenTelemetry packages. Emission is zero-cost while nothing is listening, so it is always on and stays
silent until you subscribe.

Register the client's `ActivitySource` and `Meter` by name. Use the `FunctionsDiagnostics.SourceName`
constant rather than hardcoding the string, so a typo becomes a compile error instead of a silent
no-op:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Supabase.Functions;

// Requires OpenTelemetry.Extensions.Hosting and an exporter package (e.g. OTLP) in your app.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(FunctionsDiagnostics.SourceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(FunctionsDiagnostics.SourceName)
        .AddOtlpExporter());
```

Once subscribed you get:

- A client span per invocation, named `{METHOD} {path}` and following OpenTelemetry HTTP conventions
  (method, status code, and a sanitized URL — the query string is never recorded, and neither is the
  request body). A `faas.invoked_name` tag carries the invoked function name. A relay error is
  reported as a failed span even when the HTTP status is a success.
- A `supabase.functions.invoke.duration` histogram (seconds), tagged with method, host, path,
  function name, and status code.

If you are not using the OpenTelemetry SDK, a raw listener works too:

```csharp
using System.Diagnostics;
using Supabase.Functions;

using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == FunctionsDiagnostics.SourceName,
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
