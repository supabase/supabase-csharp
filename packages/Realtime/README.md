# Supabase.Realtime

[![Build and Test](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase.Realtime)](https://www.nuget.org/packages/Supabase.Realtime/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)

A C# client for [Supabase Realtime](https://supabase.com/docs/guides/realtime) — listen to Postgres
changes, and use Broadcast and Presence over a single websocket connection. It is a C#-ification of
[realtime-js](https://github.com/supabase/realtime-js).

Part of the [Supabase C# SDK](https://github.com/supabase/supabase-csharp). Most projects
use it through the [`Supabase`](../Supabase/README.md) meta-package (`supabase.Realtime`); reference
this package directly to use Realtime on its own.

## Installation

```sh
dotnet add package Supabase.Realtime
```

Targets .NET Standard 2.0 and 2.1.

## Getting started

`ConnectAsync()` and `Subscribe()` are awaitable, so you can be sure a connection exists before
interacting with it. On the initial connection the client is **fail-fast** — `ConnectAsync()` throws a
`RealtimeException` if the socket server is unreachable. Once connected, it reconnects indefinitely
until disconnected.

```csharp
using Supabase.Realtime;

var client = new Client("ws://realtime-dev.localhost:4000/socket");
await client.ConnectAsync();

// Shorthand for a postgres_changes subscription: database, schema, table
var channel = client.Channel("realtime", "public", "todos");

channel.AddPostgresChangeHandler(ListenType.Updates, (_, change) =>
{
    var updated = change.Model<Todo>();
    var previous = change.OldModel<Todo>();
});

await channel.Subscribe();
```

`Todo` derives from `Supabase.Postgrest.Models.BaseModel`, letting the client coerce change payloads
into your model via `change.Model<T>()`.

Full generated API reference:
[Supabase.Realtime](https://supabase-community.github.io/supabase-csharp/api/Supabase.Realtime.Client.html).

## Postgres changes

Listen to inserts, updates, and deletes on a table, delivered to authorized clients according to your
[Row Level Security](https://supabase.com/docs/guides/auth/row-level-security) policies. The table
must belong to the `supabase_realtime` publication.
[More on Postgres changes](https://supabase.com/docs/guides/realtime#postgres-changes).

```csharp
var channel = client.Channel("public-users");
channel.Register(new PostgresChangesOptions("public", "users"));

channel.AddPostgresChangeHandler(ListenType.All, (_, change) =>
{
    switch (change.Event)
    {
        case EventType.Insert: /* row created */ break;
        case EventType.Update: /* row updated */ break;
        case EventType.Delete: /* row deleted */ break;
    }
});

await channel.Subscribe();
```

## Broadcast

Publish-subscribe messaging: a client sends messages to a named channel, and any other client
subscribed to that channel receives them in real time — useful for ephemeral, high-frequency data
like cursor positions. [More on Broadcast](https://supabase.com/docs/guides/realtime#broadcast).

Given a typed broadcast model:

```csharp
class MouseBroadcast : BaseBroadcast<MouseStatus> { }

class MouseStatus
{
    [JsonProperty("mouseX")] public float MouseX { get; set; }
    [JsonProperty("mouseY")] public float MouseY { get; set; }
    [JsonProperty("userId")] public string UserId { get; set; }
}
```

**Receive** typed broadcast events:

```csharp
var channel = client.Channel("cursor");
var broadcast = channel.Register<MouseBroadcast>(broadcastSelf: true);

broadcast.AddBroadcastEventHandler((_, _) =>
{
    var state = broadcast.Current();
    Debug.WriteLine($"{state.Payload.MouseX}:{state.Payload.MouseY}");
});

await channel.Subscribe();
```

**Send** a broadcast event on the same `broadcast` handle:

```csharp
await broadcast.Send("cursor", new MouseBroadcast
{
    Payload = new MouseStatus { MouseX = 123, MouseY = 456 }
});
```

## Presence

Presence tracks shared state across clients using a CRDT: each client publishes its own state, and
all subscribers converge on the same view. When a client disconnects, its state is removed
automatically — which makes "who's online" features straightforward.
[More on Presence](https://supabase.com/docs/guides/realtime#presence).

Given a typed presence model:

```csharp
class UserPresence : BasePresence
{
    [JsonProperty("lastSeen")] public DateTime LastSeen { get; set; }
}
```

**Receive** presence sync events:

```csharp
var presenceId = Guid.NewGuid().ToString();
var channel = client.Channel("last-seen");
var presence = channel.Register<UserPresence>(presenceId);

presence.AddPresenceEventHandler(EventType.Sync, (_, _) =>
{
    foreach (var state in presence.CurrentState)
    {
        var userId = state.Key;
        var lastSeen = state.Value.First().LastSeen;
        Debug.WriteLine($"{userId}: {lastSeen}");
    }
});

await channel.Subscribe();
```

**Track** this client's presence:

```csharp
presence.Track(new UserPresence { LastSeen = DateTime.Now });
```

## Events and logging

Event handlers are delegates, scoped to the object they concern — socket handlers receive
connectivity events, channel handlers receive join/leave events, and so on. Register and remove them
with the `Add`/`Remove`/`Clear` methods (e.g. `RealtimeSocket.AddStateChangedHandler`,
`RealtimeChannel.AddPostgresChangeHandler`, `RealtimeBroadcast.AddBroadcastEventHandler`).

For logging, attach a debug handler rather than relying on console output:

```csharp
client.AddDebugHandler((sender, message, exception) => Debug.WriteLine(message));
```

> **Observability:** unlike the HTTP-based Supabase clients, Realtime is **not yet** instrumented for
> OpenTelemetry, so no websocket traces or metrics are emitted.

## Contributing

Contributions are welcome. See the [repository root](https://github.com/supabase/supabase-csharp)
for how to build and test the SDK.

Note that the Realtime test suite expects `realtime-dev.localhost` to resolve locally — add a hosts
entry for `127.0.0.1  realtime-dev.localhost`.

## License

[MIT](../../LICENSE)
