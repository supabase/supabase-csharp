# Sources per area

Two read-only checkouts sit next to this repository. Default locations, overridable by the maintainer:

| Checkout | Default path | Repository |
|---|---|---|
| Capability matrix | `~/supabase/sdk/packages/capability-matrix` | `supabase/sdk` |
| supabase-js | `~/supabase/sdk-js/supabase-js/packages/core` | `supabase/supabase-js` |

If one is missing, clone it shallow into a scratch directory (`gh repo clone <repo> <dir> -- --depth 1`). Pull before starting a feature: both move. Never write to either.

## Capability matrix layout

- `capabilities/<area>.yaml` — canonical id, name, description, group.
- `specs/<area>/<group>/<method>.md` — optional normative spec; its `## API` section links the API spec and lists the operations. Only a minority of features have one.
- `README.md`, `docs/capability-matrix.md` — status semantics and how annotations map to `sdk-compliance.yaml`.

## Per area

| Area | C# package | Client surface | API spec (contract) | supabase-js (behavior example) |
|---|---|---|---|---|
| `auth` | `packages/Gotrue` | `IGotrueClient`, `IGotrueAdminClient`, `IGotrueStatelessClient` | `https://github.com/supabase/auth/blob/master/openapi.yaml` | `auth-js/src/GoTrueClient.ts`, `GoTrueAdminApi.ts`, `lib/types.ts` |
| `storage` | `packages/Storage` | `IStorageClient`, `IStorageBucketApi`, `IStorageFileApi` | `https://supabase.github.io/storage/` | `storage-js/src/` (`packages/` per API family, `lib/`) |
| `database` | `packages/Postgrest` | `IPostgrestClient`, `IPostgrestTable` | PostgREST reference, `https://docs.postgrest.org/en/latest/references/api.html` | `postgrest-js/src/Postgrest*Builder.ts` |
| `realtime` | `packages/Realtime` | `IRealtimeClient`, `IRealtimeChannel`, `IRealtimePresence`, `IRealtimeBroadcast` | No OpenAPI: Phoenix channel protocol, `https://github.com/supabase/realtime` | `realtime-js/src/RealtimeClient.ts`, `RealtimeChannel.ts`, `RealtimePresence.ts` |
| `functions` | `packages/Functions` | `IFunctionsClient` | No OpenAPI: `POST/GET … /functions/v1/<name>`, `https://supabase.com/docs/guides/functions` | `functions-js/src/FunctionsClient.ts` |
| `client` | `packages/Supabase` (+ `packages/Core`) | `ISupabaseClient` | — (composition of the above) | `supabase-js/src/SupabaseClient.ts` |

The API spec for a capability is the one its spec file links when it has one; otherwise the area's row above. Find the operation by path or `operationId`, not by the JS method name.

## In this repository

- `supabase/config.toml` — the local/CI stack; enable services a feature needs here.
- `packages/<Name>/<Name>.Tests/Support/` — the package's WireMock server, `RequestApprovalFixture`, E2E client factories. Reuse; never add a parallel fixture.
- `packages/<Name>/<Name>/Exceptions/` — the typed exception and its reason/hint enum.
- `packages/<Name>/<Name>/PublicAPI.Unshipped.txt` — where new public surface is declared.
- `packages/Core/Core/Attributes/CapabilityAttributes.cs` — `[ImplementedCapability]` / `[PartialCapability]`.
