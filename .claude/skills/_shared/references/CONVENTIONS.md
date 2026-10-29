# C# SDK Conventions

The quality bar we are building **toward**. This is a target spec, benchmarked against best-in-class .NET SDKs (Azure SDK, Stripe.net) and the maintainer's Vonage .NET SDK, not a description of the code as it stands today. Where a rule and the existing code disagree, the rule is right and the code is debt: new and changed code meets the bar; untouched code is migrated deliberately, not churned.

Some rules can only land with a breaking release. They are marked **v9** (the next major; v8 is current) and new v8 code follows the **v8** column instead — introducing them piecemeal would leave callers with two contracts side by side.

> Companion: [QUALITY_RUBRIC.md](QUALITY_RUBRIC.md) defines when a change is *done*. Each section notes the **From → To** so the direction is explicit.

---

## 0. Guiding principles

These decide any call the specific rules don't cover:

1. **Developer experience is the top priority.** A developer must be able to use the SDK correctly from IntelliSense, without reading its source — via clear types, predictable patterns, and examples in the docs.
2. **"Familiar" means the Supabase house style, expressed in idiomatic C#.** The end state is the transparent, `{ data, error }`-style contract Supabase developers know across every Supabase SDK, honored through C#'s type system (§9, v9). Until then, new surface stays consistent with the v8 surface beside it.
3. **Lean on the type system.** Make illegal states unrepresentable: immutability by default, results and optionality expressed as types rather than nulls, exceptions, or convention. C# is a functional language — use it.
4. **Typed contracts over stringly-typed anything.** Requests and responses are types, not dictionaries.
5. **Code reads like a story.** Orchestration says *what* it does (intent), not *how*; mechanics live below in named helpers (§5.5). This is a first-class readability requirement, not a nicety.
6. **The reference SDK's *behavior* is the spec; the *shape* is ours.** Port what supabase-js does, expressed idiomatically (§12).

## 1. Project & target frameworks

- **The monorepo cutover is done and v8 has shipped.** Library packages target `netstandard2.1`; test projects target `net10.0`. New code may assume the `netstandard2.1` feature set (`IAsyncEnumerable<T>`, nullable annotations, `Span<T>`); `PolySharp` supplies `init`/`record` support.
- **Never use APIs missing from `netstandard2.1`** in library code.

## 2. Solution & folder structure — vertical slices

**From:** flat per-package folders (`Gotrue/*.cs`) mixing clients, models, and options.
**v8:** new types for a capability go in a folder named after the capability's group, next to the existing ones (`Gotrue/Mfa/`, `Gotrue/Claims/`, `Gotrue/Resend/`); the operation is exposed on the client interface that already owns that group.
**To (v9):** one folder per **operation** (Vonage-style vertical slices):

```
<Feature>/
  I<Feature>Client.cs        // public interface
  <Feature>Client.cs         // internal implementation
  <Operation>/
    <Operation>Request.cs    // immutable request, IRequest
    <Operation>Response.cs   // immutable response
```

- Clients are `internal` classes behind public interfaces (`I<Feature>Client`).
- Each operation is self-contained and is the unit the parity workflow and any future codegen produce.

## 3. Language & nullability

- Nullable reference types are `enable`d everywhere; a nullable warning is a bug.
- Model optionality precisely (`T?` vs non-nullable-and-initialized). Prefer a `Maybe<T>`/optional type over `null` where "absent" is a meaningful state (§9).
- File-scoped namespaces, one public type per file, filename = type name. **Spaces, indent 4** (per the repo-wide `.editorconfig`, which is the machine-enforced source of truth — §13); `dotnet format --verify-no-changes` is the gate. *(The repo-wide config has landed and the former tab-indented packages, e.g. gotrue, are migrated.)*

## 4. Naming

- Standard .NET naming: `PascalCase` types/members, `camelCase` private fields, `IPascalCase` interfaces. **From → To:** the `_camelCase` underscore-prefix convention is retired — new and changed code uses a plain `camelCase` field name (no `_`); existing underscore-prefixed fields are debt, migrated deliberately, not churned.
- **From → To:** existing async methods have no suffix. **New async public methods use the `Async` suffix** (`SignUpAsync`); the suffix-free surface is renamed at the breaking major. Don't mix styles within a new surface.
- Names come from .NET/Supabase vocabulary, not JS transliteration (a JS `onAuthStateChange` callback → a .NET `event`/`IObservable`/`IAsyncEnumerable`).

**Async & cancellation (library correctness):**
- **`ConfigureAwait(false)` on every await in library code.** We ship a library, not app code — never capture a caller's synchronization context. Non-negotiable on new/changed code.
- **Honor the `CancellationToken`, don't just accept it.** Thread it through the entire call path (transport, retries, streaming) and let it actually cancel in-flight I/O. A token parameter that's swallowed is worse than none — it lies to the caller (QUALITY_RUBRIC §7).

## 5. Models, requests & responses — immutable, typed, per-operation

**From:** mutable `{ get; set; }` classes with `[JsonProperty]`, one bag reused for send and receive.
**To:**

- **Immutable is mandatory.** Prefer `record` types (or `readonly struct` for small value-like requests, à la Vonage) with `init`-only members.
- **Separate request and response types per operation** (§2). A single type for both is allowed only when it is *not* an anemic DTO — i.e. it carries real behavior/invariants.
- **v9:** a request owns its HTTP representation via `IRequest.BuildRequestMessage()` (§8). **v8:** requests go through the package's existing transport helper (e.g. `Helpers.MakeRequestAsync` in Gotrue).
- Collections are exposed read-only (`IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`) and never null.
- **Domain primitives over raw strings** where the SDK validates anyway: `Email`, `PhoneNumber`, etc. — parse, don't stringly-type.
- **Serializer is System.Text.Json.** The Newtonsoft → STJ migration is complete SDK-wide (no Newtonsoft references remain in `packages/`). Keep serialization at the model/transport boundary. Map wire names with the serializer's snake_case convention; the C# surface is PascalCase.

## 5.5. Declarative composition — code reads like a story

The single most important readability rule, and a defining trait of the Vonage SDK we're modelling on.

- **Orchestration methods read top-to-bottom as a sequence of named intentions**, not a wall of mechanics. The Vonage `SimSwapClient` is the reference: `request.Map(BuildAuthorizeRequest).BindAsync(SendAuthorizeRequest).Map(BuildAuthenticateResponse)` — each step names *what* happens; the *how* is a private method below.
- **Push implementation details down.** Loops, parsing, header assembly, string building, null/absence handling — extract into well-named private methods/functions so the high-level method stays a narrative of intent.
- **Name for intent, not mechanism** (`BuildAuthenticationHeader`, not `ConcatTokenString`).
- This pairs with the return-based composition of §9 (`Map`/`Bind` pipelines, v9) — the error model and the declarative style reinforce each other.
- **No comments in code.** A method you can't read as prose without stepping into it is a signal to extract and rename, not to add a comment. XML docs on public surface are documentation, not comments, and are required (§11).

## 5.6. Test code — held to the production bar

Tests are code a developer reads to learn the surface; they get the same care as shipping code.

- **Name by contract:** `Sut_ShouldConsequence_GivenScenario`, or `Sut_ShouldConsequence` when there is no scenario. The method name *is* the spec — do not restate it in a `[TestMethod(DisplayName = …)]`; a redundant display name is noise. (`[DataTestMethod]` is obsolete: use `[TestMethod]` + `[DataRow]`.) The *why* lives in the FluentAssertions reason string, citing the spec/RFC/issue.
- **Class name states the element + scenario (the use case), not the tier or a mechanism.** The `[TestCategory(...)]` conveys the tier, so the name describes *what is under test*: the element and, where it varies, the scenario — `StorageBucketTests` / `StorageBucketAnonTests` / `StorageBucketUnauthenticatedTests`. Never a tier (`ClientSmokeE2ETests`) and never a cross-cutting *mechanism* (`ExceptionTests`, `AuthorizationTests`) — a mechanism name scatters an element's behaviour across the suite. If the tests probe `CreateBucket`, they belong in the bucket element's scenario family, not a theme bucket. A class `<summary>` says what you'll find inside.
- **Lay the test project out by domain, not by tier — a screaming architecture.** Folders, and matching nested namespaces (as the production project does — `Storage/Extensions`, `Storage/Exceptions`), mirror the element/concern under test — `Buckets/`, `Files/`, `Errors/`, `Options/`, `Observability/` — so a reader finds *everything about buckets* in one place instead of scanning a flat pile. **Do not** create `Unit/`, `Contract/`, `E2E/` folders: the tier is already carried by `[TestCategory]` + the class name, and a tier-first layout screams the test taxonomy rather than the domain (the anti-pattern). The project root holds only shared test support (`Helpers`, `TestConventions`) and assets. A folder must not be named after a type in scope — a `Client/` folder becomes a `…Client` namespace that shadows the `Client` type (`CS0118`); name it for the concern (`Headers/`).
- **Substitute, don't hand-roll.** Use **NSubstitute** for collaborators; never hand-written stub/fake classes — they carry uncovered lines and break the moment an interface gains a member. Capture callbacks with `sub.When(...).Do(...)`; verify boundary effects with `Received()` / `DidNotReceive()`.
- **Tight and inline.** No blank lines between the arrange/act/assert phases; assert directly on the expression (`sut.Call().Should()...`) rather than parking it in a throwaway local. FluentAssertions throughout, with an `AssertionScope` when several facets of one outcome are asserted together.

## 6. Options & construction

- Optional inputs go in an immutable `XxxOptions` (init-only), passed as a single defaulted parameter with safe defaults, so passing nothing reproduces prior behavior (keeps new options additive — §7).
- **Core clients are constructable without a DI container.** A public constructor / factory, no service-locator assumptions — usable directly from a console app, Unity, or any host. **Core packages take no `Microsoft.Extensions.*` dependency** (DI, `IHttpClientFactory`, options binding): those drag in framework-flavored deps that make no sense outside ASP.NET. Container registration (`services.AddSupabase(...)`) ships as a separate `Supabase.Extensions.DependencyInjection` package (`packages/extensions/DependencyInjection`) that layers on top of these plain constructors — the constructability seam is what keeps that possible.
- **Fluent builders with compile-time-safe mandatory fields** (the Vonage `[Builder]`/`[Mandatory]`/`[ValidationRule]` source generator) are the target ergonomics for request construction — **deferred to v9 with the error model (§9)**, because a builder must not throw. Until then: immutable `init`-only records.

## 7. Public API design (additive today, idiomatic always)

- **Additive / opt-in only until v9.** New members/overloads/opt-in options with safe defaults. Changing a signature, default, serialized shape, or error contract on an existing member is breaking — batch it into the planned major with maintainer sign-off.
- **New surface is built to the target now**, not to match the legacy code beside it.
- **Deprecation discipline:** retire surface with `[Obsolete("… Favor <new API> instead.")]` pointing at the replacement, and record it in `MIGRATION_v9.md`.
- Expose capabilities on the interface, not only the concrete class. XML docs and `<example>` live on the interface member; the implementation uses `<inheritdoc/>`.
- **Encapsulation by default.** Implementation classes, helpers and wire DTOs that callers never construct are `internal`. A type becomes public only when a caller needs to name it.

## 8. Transport — typed, generic, centralized (v9)

**From:** `Dictionary<string, object>` bodies built inline and per-method deserialization.
**v8:** reuse the package's existing transport helper; send typed request records, never dictionaries.
**To (v9, Vonage shape):**

- One generic transport — `HttpClient<TError>.SendAsync<TRequest, TResponse>(request)` — owns headers, base-URL composition, serialization, error mapping (§9), and OTel. Call sites describe *what*, not *how*.
- Requests implement `IRequest.BuildRequestMessage()`; responses are typed models. No stringly-typed dictionaries in call implementations.
- This layer is the seam the codegen objective plugs into — treat request/response types and transport as if they could be generated from OpenAPI.
- Standard headers (`X-Client-Info` with assembly version) and dynamic per-request headers are the transport's concern.

## 9. Error model

**v8 (current — all new surface follows this):** operation failures throw the package's typed exception (`GotrueException` with `FailureReason`, `SupabaseStorageException`, `PostgrestException`, `FunctionsException`, `RealtimeException`), mapped by the package's existing error-handling path. A new failure mode extends the existing reason/hint enum rather than inventing a new exception type. Argument misuse throws `ArgumentException` before any I/O.

**v9 (target — do not introduce piecemeal):** failures are **values in the return type** — the Supabase `{ data, error }` house style as a `Result`-style return, with a typed failure hierarchy (Vonage-style: `HttpFailure`, `ParsingFailure`, `AuthenticationFailure`, …). It ships for the whole SDK at once in v9; shipping it on individual v8 methods would leave callers with two error contracts.
- Never put secrets/tokens/full auth URLs in failures, spans, or logs — route URLs through `UrlSanitizer`.

## 10. Observability

- New I/O participates in the existing OpenTelemetry `Activity` instrumentation (`Supabase.Core` `Diagnostics`/`Instrumentation`, per-package `*Diagnostics`), not ad-hoc logging.

## 11. Documentation (part of DX)

- Every public **type and member** has an XML `<summary>`; parameters, returns, and failures are documented when not self-evident.
- **Primary entry points carry an `<example>`** (Vonage does this pervasively — IntelliSense reads like a tutorial). `GenerateDocumentationFile` is on; a missing doc on public API is a warning we fix, not suppress.

## 12. Parity mapping principle

Behavior comes from the capability's **contract**: the capability-matrix spec when one exists, the service's API spec (OpenAPI) for the wire, and supabase-js (auth-js, storage-js, postgrest-js, realtime-js, functions-js) as a worked example of the SDK-layer behavior. Match what the reference *does*, not how it is written.

- **supabase-js is inspiration, never source.** Do not port, transliterate or adapt JS code. Read it to learn the behavior (defaults, validation before the wire, state changes, edge cases), then design the C# from the contract. Not all of it meets this bar: where the JS is loose (stringly-typed unions, untyped bags, swallowed errors), the C# does better and the PR says so.
- JS callbacks → .NET events/`IAsyncEnumerable`; JS options bags → immutable `XxxOptions` records; JS `{ data, error }` → the return value plus the typed exception (v8), the result type (v9); JS loose unions → enums or overloads.

## 13. Tooling & enforcement

- Repo-wide `.editorconfig` (seeded from the Vonage SDK's) and `.globalconfig` enforce formatting, naming and analyzer severities; `dotnet format` is the tie-breaker for anything unstated here.
- Analyzers: the .NET analyzers at `AnalysisLevel` latest, plus `Microsoft.CodeAnalysis.PublicApiAnalyzers` tracking the public surface in `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`.
- **`TreatWarningsAsErrors` flips on per package once it reaches zero warnings** — already on for `Core`; the remaining packages still carry warning debt. New and changed code introduces **zero new warnings** so the debt only shrinks. See QUALITY_RUBRIC §4.
