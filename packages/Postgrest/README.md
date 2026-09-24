# Supabase.Postgrest

[![Build and Test](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase.Postgrest)](https://www.nuget.org/packages/Supabase.Postgrest/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)

A C# client for [PostgREST](https://postgrest.org) — query your Supabase database through the
auto-generated REST API, with strongly-typed models and LINQ. It is a C#-ification of
[postgrest-js](https://github.com/supabase/postgrest-js).

Part of the [Supabase C# SDK](https://github.com/supabase/supabase-csharp). Most projects
use it through the [`Supabase`](../Supabase/README.md) meta-package (`supabase.From<T>()`); reference
this package directly to use PostgREST on its own. It also works outside the Supabase ecosystem
against any PostgREST server.

## Installation

```sh
dotnet add package Supabase.Postgrest
```

Targets .NET Standard 2.0.

## Getting started

Every table maps to a model deriving from `BaseModel`. Use `Table`, `PrimaryKey`, and `Column`
attributes to map C# names to their database counterparts:

```csharp
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("messages")]
public class Message : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("username")]
    public string UserName { get; set; }

    [Column("channel_id")]
    public int ChannelId { get; set; }
}
```

Create a client and work with a model through `Table<T>()`:

```csharp
using Supabase.Postgrest;

var client = new Client("http://localhost:3000");

// Read
var response = await client.Table<Message>().Get();
List<Message> messages = response.Models;

// Insert
await client.Table<Message>().Insert(new Message { UserName = "acupofjose", ChannelId = 1 });

// Update (via a fetched model)
var message = response.Models.First();
message.UserName = "elrhomariyounes";
await message.Update<Message>();

// Delete
await response.Models.Last().Delete<Message>();
```

Against the Supabase hosted service, pass your keys as headers when constructing the client:

```csharp
var options = new ClientOptions();
var client = new Client("https://PROJECT_ID.supabase.co/rest/v1", options)
{
    GetHeaders = () => new Dictionary<string, string>
    {
        { "apikey", SUPABASE_PUBLIC_KEY },
        { "Authorization", $"Bearer {SUPABASE_USER_TOKEN}" }
    }
};
```

### LINQ

Filters, ordering, and projections are expressed as LINQ over your model:

```csharp
await client.Table<Movie>()
            .Select(x => new object[] { x.Id, x.Name, x.Tags, x.ReleaseDate })
            .Where(x => x.Tags.Contains("Action") || x.Tags.Contains("Adventure"))
            .Order(x => x.ReleaseDate, Ordering.Descending)
            .Get();

await client.Table<Movie>()
            .Set(x => x.WatchedAt, DateTime.Now)
            .Where(x => x.Id == "11111-22222-33333-44444")
            .Update();
```

Full generated API reference:
[Supabase.Postgrest](https://supabase-community.github.io/supabase-csharp/api/Supabase.Postgrest.html).

## Foreign keys, join tables, and relationships

PostgREST introspects relationships between tables and can return related rows inline. **Foreign key
constraints are required for PostgREST to detect these relationships.** Mark a related property with
the `Reference` attribute to include it:

- [One-to-one](https://postgrest.org/en/stable/api.html#one-to-one-relationships) — detected from a
  unique constraint on a foreign key.
- [One-to-many](https://postgrest.org/en/stable/api.html#one-to-many-relationships) — the inverse of
  a foreign key reference.
- [Many-to-many](https://postgrest.org/en/stable/api.html#many-to-many-relationships) — detected via
  a join table whose composite key contains foreign keys to both tables.

```csharp
[Table("movie")]
public class Movie : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Reference(typeof(Person))]
    public List<Person> Persons { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("person")]
public class Person : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; }

    [Column("last_name")]
    public string LastName { get; set; }

    [Reference(typeof(Profile))]
    public Profile Profile { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("profile")]
public class Profile : BaseModel
{
    [PrimaryKey("person_id")]
    public int PersonId { get; set; }

    [Column("email")]
    public string Email { get; set; }
}
```

**Each related model must inherit `BaseModel` and specify its `Table` and `Column` attributes.** By
default a `Reference` is included in all GET queries on the table (this can be disabled in its
constructor). Querying `Movie` given the above returns each movie with its `person` array, and each
person with their nested `profile`.

### Circular references

Circular relations are allowed but are only parsed one level deep. Given circular models, a `Person`
returned under a `Movie` will itself carry the root `Movie`, and a `Person -> Profile` carries its
root `Person`. If that is undesirable, define dedicated join models without the circular references.

### Top-level filtering

**By default** a relation acts as a top-level (inner-join) filter: a `Movie` with no related `Person`
would not be returned. Set `useInnerJoin: false` to keep returning the parent even when the relation
is empty:

```csharp
[Table("movie")]
public class Movie : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Reference(typeof(Person), useInnerJoin: false)]
    public List<Person> People { get; set; } = new();
}
```

### Inserting related records

PostgREST _does not support nested inserts or upserts_ — a request writes to exactly one table. So
`Reference` properties are **ignored** on insert, update, and upsert, for every relationship type.
Inserting a `Movie` with its `Persons` list populated persists the movie and writes nothing else,
without raising an error.

To create a relationship, write the foreign key where it lives in the database:

**One-to-one / many-to-one** — the foreign key is a column on the row being inserted. Expose it on the
model (as `Profile` does with `person_id`) and set it directly:

```csharp
// `profile.person_id` references `person.id` — setting the column creates the relationship.
await client.Table<Profile>().Insert(new Profile { PersonId = person.Id, Email = "tom.cruise@supabase.io" });
```

**One-to-many** — the foreign key is a column on each child row. Insert the parent first (by default
the response contains the inserted record, including database-generated values such as its primary
key), then bulk-insert the children with their foreign key column set to the parent's key:

```csharp
[Table("review")]
public class Review : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("movie_id")]
    public int MovieId { get; set; }

    [Column("content")]
    public string Content { get; set; }
}
```

```csharp
// 1. Insert the parent and retrieve its database-generated key.
var response = await client.Table<Movie>().Insert(new Movie { Name = "Top Gun: Maverick" });
var movie = response.Model!;

// 2. Bulk-insert the children with their foreign key column set.
var reviews = contents
    .Select(content => new Review { MovieId = movie.Id, Content = content })
    .ToList();
await client.Table<Review>().Insert(reviews);
```

**Many-to-many** — the foreign keys live in a join table that has no counterpart in your domain
model, so it must be modeled and written explicitly:

```csharp
[Table("movie_person")]
public class MoviePerson : BaseModel
{
    [Column("movie_id")]
    public int MovieId { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
}
```

```csharp
// 1. Insert the root record and retrieve its database-generated key.
var response = await client.Table<Movie>().Insert(new Movie { Name = "Top Gun: Maverick" });
var movie = response.Model!;

// 2. Insert all join rows in a single bulk request.
var moviePersons = persons
    .Select(person => new MoviePerson { MovieId = movie.Id, PersonId = person.Id })
    .ToList();
await client.Table<MoviePerson>().Insert(moviePersons);
```

Writes across multiple requests are **not atomic** — if a later request fails, the earlier records
exist without their relationships. When atomicity matters, wrap the writes in a
[database function](https://supabase.com/docs/guides/database/functions) and call it through `Rpc`:

```csharp
await client.Rpc("insert_movie_with_persons", new Dictionary<string, object>
{
    { "name", "Top Gun: Maverick" },
    { "person_ids", persons.Select(p => p.Id).ToList() }
});
```

## Observability (OpenTelemetry)

The client emits traces and metrics through `System.Diagnostics`, so you can wire them into
OpenTelemetry (or any `ActivityListener` / `MeterListener`) without taking a dependency on the
OpenTelemetry packages. Emission is zero-cost while nothing is listening, so it is always on and stays
silent until you subscribe.

Register the client's `ActivitySource` and `Meter` by name. Use the `PostgrestDiagnostics.SourceName`
constant rather than hardcoding the string, so a typo becomes a compile error instead of a silent
no-op:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Supabase.Postgrest;

// Requires OpenTelemetry.Extensions.Hosting and an exporter package (e.g. OTLP) in your app.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(PostgrestDiagnostics.SourceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(PostgrestDiagnostics.SourceName)
        .AddOtlpExporter());
```

Once subscribed you get:

- A client span per request, named `{METHOD} {path}` and following OpenTelemetry HTTP conventions
  (method, status code, and a sanitized URL). The query string is **never** recorded — in PostgREST
  it carries the column filters and their values, which are potential PII. A `db.operation` tag
  (`select`, `insert`, `update`, `upsert`, `delete`, `count`, `rpc`) distinguishes the logical
  operation, since several map to the same HTTP verb.
- A `supabase.postgrest.http.request.duration` histogram (seconds), tagged with method, host, path,
  operation, and status code.

If you are not using the OpenTelemetry SDK, a raw listener works too:

```csharp
using System.Diagnostics;
using Supabase.Postgrest;

using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == PostgrestDiagnostics.SourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity => Console.WriteLine($"{activity.OperationName} {activity.Duration.TotalMilliseconds}ms {activity.Status}")
};
ActivitySource.AddActivityListener(listener);
```

## Contributing

Contributions are welcome. See the [repository root](https://github.com/supabase/supabase-csharp)
for how to build and test the SDK.

## License

[MIT](../../LICENSE)
