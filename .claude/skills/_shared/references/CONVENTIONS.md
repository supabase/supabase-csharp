# C# SDK Conventions

The quality bar we are building **toward**. This is a target spec, benchmarked against best-in-class .NET SDKs (Azure SDK, Stripe.net) and the maintainer's Vonage .NET SDK, not a description of the code as it stands today. Where a rule and the existing code disagree, the rule is right and the code is debt: new and changed code meets the bar; untouched code is migrated deliberately, not churned.

> Companion: [QUALITY_RUBRIC.md](QUALITY_RUBRIC.md) defines when a change is *done*. Each section notes the **From → To** so the direction is explicit.

---

## 0. Guiding principles

These decide any call the specific rules don't cover:

1. **Developer experience is the top priority.** A developer must be able to use the SDK correctly from IntelliSense, without reading its source — via clear types, predictable patterns, and examples in the docs.
2. **"Familiar" means the Supabase house style, expressed in idiomatic C#.** Familiar DX is *not* "throws like other .NET SDKs"; it's the transparent, `{ data, error }`-style contract Supabase developers know across every Supabase SDK, honored through C#'s type system (§9).
3. **Lean on the type system.** Make illegal states unrepresentable: immutability by default, results and optionality expressed as types rather than nulls, exceptions, or convention. C# is a functional language — use it.
4. **Typed contracts over stringly-typed anything.** Requests and responses are types, not dictionaries.
5. **Code reads like a story.** Orchestration says *what* it does (intent), not *how*; mechanics live below in named helpers (§5.5). This is a first-class readability requirement, not a nicety.
6. **The reference SDK's *behavior* is the spec; the *shape* is ours.** Port what supabase-js does, expressed idiomatically (§12).

## 1. Project & target frameworks

- **The monorepo cutover is done and the major (v8) has shipped.** Packages now target `netstandard2.1` as the reach baseline plus `net10.0`. New code may assume the `netstandard2.1` feature set (`IAsyncEnumerable<T>`, full nullable annotations, `Span<T>`).
- **Never assume net10-only APIs** in library code; guard with `#if` or use a `netstandard2.1` equivalent so the baseline target still compiles.

## 2. Solution & folder structure — vertical slices

**From:** flat per-package folders (`Gotrue/*.cs`) mixing clients, models, and options.
**To:** one folder per **operation** (Vonage-style vertical slices):

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
- A request owns its HTTP representation: implement `IRequest.BuildRequestMessage()` (§8).
- Collections are exposed read-only (`IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`) and never null.
- **Domain primitives over raw strings** where the SDK validates anyway: `Email`, `PhoneNumber`, etc. — parse, don't stringly-type.
- **Serializer is System.Text.Json.** The Newtonsoft → STJ migration is complete SDK-wide (no Newtonsoft references remain in `packages/`). Keep serialization at the model/transport boundary. Map wire names with the serializer's snake_case convention; the C# surface is PascalCase.

## 5.5. Declarative composition — code reads like a story

The single most important readability rule, and a defining trait of the Vonage SDK we're modelling on.

- **Orchestration methods read top-to-bottom as a sequence of named intentions**, not a wall of mechanics. The Vonage `SimSwapClient` is the reference: `request.Map(BuildAuthorizeRequest).BindAsync(SendAuthorizeRequest).Map(BuildAuthenticateResponse)` — each step names *what* happens; the *how* is a private method below.
- **Push implementation details down.** Loops, parsing, header assembly, string building, null/absence handling — extract into well-named private methods/functions so the high-level method stays a narrative of intent.
- **Name for intent, not mechanism** (`BuildAuthenticationHeader`, not `ConcatTokenString`).
- This pairs with the return-based composition of §9 (`Map`/`Bind` pipelines) — the error model and the declarative style reinforce each other.
- A method you can't read as prose without stepping into it is a signal to extract and rename, not to add a comment.

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
- **Fluent builders with compile-time-safe mandatory fields** (the Vonage `[Builder]`/`[Mandatory]`/`[ValidationRule]` source generator) are the target ergonomics for request construction — **deferred until the error model lands (§9)**, because a builder must not throw. Until then: immutable requests with `init` + a result-returning validation step.

## 7. Public API design (additive today, idiomatic always)

- **Additive / opt-in only until the major.** New members/overloads/opt-in options with safe defaults. Changing a signature, default, serialized shape, or error contract on an existing member is breaking — batch it into the planned major with maintainer sign-off.
- **New surface is built to the target now**, not to match the legacy code beside it.
- **Deprecation discipline:** retire surface with `[Obsolete("… Favor <new API> instead.")]` pointing at the replacement, and record it in a per-major `MIGRATION_vN.md`. This is the mechanism that lets the return-based API (§9) coexist with the old exception surface until the major removes it.
- Expose capabilities on the interface, not only the concrete class.

## 8. Transport — typed, generic, centralized

**From:** `Dictionary<string, object>` bodies built inline and `JsonConvert.DeserializeObject<T>` per method.
**To (Vonage shape):**

- One generic transport — `HttpClient<TError>.SendAsync<TRequest, TResponse>(request)` — owns headers, base-URL composition, serialization, error mapping (§9), and OTel. Call sites describe *what*, not *how*.
- Requests implement `IRequest.BuildRequestMessage()`; responses are typed models. No stringly-typed dictionaries in call implementations.
- This layer is the seam the codegen objective plugs into — treat request/response types and transport as if they could be generated from OpenAPI.
- Standard headers (`X-Client-Info` with assembly version) and dynamic per-request headers are the transport's concern.

## 9. Error model — return-based & transparent (progressive)

This is a deliberate, phased shift. **Direction:** failures are **values in the return type**, not thrown — the Supabase `{ data, error }` house style, expressed with C#'s type system (a `Result`/monad-style return; exact type TBD when we build it). Rationale: transparency and composability, the same bet supabase-js made.

- **Fallible operations return the result type; they do not throw for expected failures.** Validation returns a result (never throws) — this is why builders wait for it (§6).
- **Typed failure hierarchy** (Vonage-style: `HttpFailure`, `ParsingFailure`, `AuthenticationFailure`, `DeserializationFailure`, …), each carrying enough to render a message, convert to an exception (escape hatch), or wrap into a result. Richer than a single `GotrueException`.
- **Progressive adoption (phasing is a maintainer-confirmed plan, not yet final):** the return-based API is introduced in `Supabase.Core`, adopted by new features and newly-touched call paths; the existing exception-throwing surface is deprecated (§7) pointing to the new one and removed at the major. We do **not** maintain two error philosophies indefinitely.
- Genuinely exceptional/programmer errors (misuse, argument validation of non-user input) may still throw `ArgumentException` etc.; the return type is for *operation* failures the caller is expected to handle.
- Never put secrets/tokens/full auth URLs in failures, spans, or logs — route URLs through `UrlSanitizer`.

## 10. Observability

- New I/O participates in the existing OpenTelemetry `Activity` instrumentation (`Supabase.Core` `Diagnostics`/`Instrumentation`, per-package `*Diagnostics`), not ad-hoc logging.

## 11. Documentation (part of DX)

- Every public **type and member** has an XML `<summary>`; parameters, returns, and failures are documented when not self-evident.
- **Primary entry points carry an `<example>`** (Vonage does this pervasively — IntelliSense reads like a tutorial). `GenerateDocumentationFile` is on; a missing doc on public API is a warning we fix, not suppress.

## 12. Parity mapping principle

Port from supabase-js — the reference monorepo (gotrue-js, realtime-js, storage-js, …) — for **behavior equivalence with an idiomatic .NET surface** — match what the reference *does*, not how its signatures look. JS callbacks → .NET events/`IAsyncEnumerable`; JS options bags → immutable `XxxOptions`; JS `{ data, error }` → the return-based result type (§9); JS loose unions → enums or overloads. Behavior is the spec; shape is ours.

## 13. Tooling & enforcement

- Repo-wide `.editorconfig` (seeded from the Vonage SDK's) enforces formatting and naming; `dotnet format` is the tie-breaker for anything unstated here.
- Analyzers (Roslynator/StyleCop) are enabled to guide toward these rules.
- **`TreatWarningsAsErrors` flips on per package once it reaches zero warnings** — already on for `Core`; the remaining packages still carry warning debt. New and changed code introduces **zero new warnings** so the debt only shrinks. See QUALITY_RUBRIC §4.
