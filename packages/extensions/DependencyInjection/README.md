# Supabase.Extensions.DependencyInjection

[![Build and Test](https://github.com/supabase-community/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase-community/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase.Extensions.DependencyInjection)](https://www.nuget.org/packages/Supabase.Extensions.DependencyInjection/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../../LICENSE)

ASP.NET Core / `Microsoft.Extensions.DependencyInjection` integration for the
[Supabase C# SDK](https://github.com/supabase-community/supabase-csharp).

Registers a `Supabase.Client` — and each of its sub-clients individually — against
`IHttpClientFactory`-managed, pooled `HttpClient`s. Without this, an app that resolves a
new `Supabase.Client` per request builds a new `HttpClient` (and socket) per request too;
under sustained load that exhausts the connection pool. `IHttpClientFactory` fixes that by
pooling the underlying `SocketsHttpHandler` across resolutions.

## Installation

```sh
dotnet add package Supabase.Extensions.DependencyInjection
```

Targets .NET Standard 2.1.

## Usage

```csharp
builder.Services.AddSupabase(
    supabaseUrl: "https://xyz.supabase.co",
    supabaseKey: builder.Configuration["Supabase:Key"]!,
    configureOptions: options =>
    {
        options.AutoConnectRealtime = true;
        options.PostgrestRetry = new RetryOptions { MaxRetries = 3 };
    });
```

This registers a scoped `Supabase.Client`, plus each sub-client individually
(`IGotrueClient<User, Session>`, `IPostgrestClient`, `IStorageClient<Bucket, FileObject>`,
`IFunctionsClient`, `IRealtimeClient<RealtimeSocket, RealtimeChannel>`), so a handler can
inject just the one it needs:

```csharp
app.MapGet("/todos", async (IPostgrestClient postgrest) =>
    await postgrest.Table<Todo>().Get());
```

Any `HttpClient`/proxy set directly via `configureOptions` is overwritten — this package
supplies those from `IHttpClientFactory` so every client's traffic goes through the pooled,
DI-managed handlers instead of a handler the SDK builds and owns itself.
