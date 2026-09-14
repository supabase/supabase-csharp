# Changelog

## [8.1.0](https://github.com/supabase-community/supabase-csharp/compare/v8.0.0...v8.1.0) (2026-09-07)


### Features

* **functions:** stream edge function responses ([#417](https://github.com/supabase-community/supabase-csharp/issues/417)) ([649d922](https://github.com/supabase-community/supabase-csharp/commit/649d92254ab8405d4b47287bf2d62632b827d2c6))
* **realtime:** add `enabled` flag for opt-in initial presence sync ([#407](https://github.com/supabase-community/supabase-csharp/issues/407)) ([ecb6b11](https://github.com/supabase-community/supabase-csharp/commit/ecb6b11546dc97e1979e71b07fed5e572987d028))


### Bug Fixes

* **postgrest:** keep '!' and unary minus on captured values in Where filters ([#414](https://github.com/supabase-community/supabase-csharp/issues/414)) ([0edc5d9](https://github.com/supabase-community/supabase-csharp/commit/0edc5d96a91e300ad8f22bfe6873f6ccb4317c30))
* **postgrest:** write int arrays as json arrays and reject bad literals with JsonException ([#408](https://github.com/supabase-community/supabase-csharp/issues/408)) ([e479c9e](https://github.com/supabase-community/supabase-csharp/commit/e479c9e65d2be03753da22b75cd8843e08a508ee))

## [8.0.0](https://github.com/supabase-community/supabase-csharp/compare/v7.4.0...v8.0.0) (2026-09-03)


### ⚠ BREAKING CHANGES

* **gotrue:** stop sending OAuth state to /authorize ([#388](https://github.com/supabase-community/supabase-csharp/issues/388))
* **realtime:** throw exception when updating postgres_changes after channel joins ([#385](https://github.com/supabase-community/supabase-csharp/issues/385))
* **postgrest:** throw on multi-row Single() result instead of returning null ([#346](https://github.com/supabase-community/supabase-csharp/issues/346))
* **gotrue:** add retry/backoff and injectable HttpClient support ([#371](https://github.com/supabase-community/supabase-csharp/issues/371))
* **supabase:** migrate from Newtonsoft.Json to System.Text.Json ([#360](https://github.com/supabase-community/supabase-csharp/issues/360))
* **realtime:** migrate from Newtonsoft.Json to System.Text.Json
* **postgrest:** migrate from Newtonsoft.Json to System.Text.Json
* **storage:** migrate from Newtonsoft.Json to System.Text.Json ([#358](https://github.com/supabase-community/supabase-csharp/issues/358))
* **gotrue:** migrate from Newtonsoft.Json to System.Text.Json ([#357](https://github.com/supabase-community/supabase-csharp/issues/357))
* **functions:** migrate from Newtonsoft.Json to System.Text.Json ([#356](https://github.com/supabase-community/supabase-csharp/issues/356))
* packages now target netstandard2.1 instead of netstandard2.0. .NET Framework consumers (netstandard2.0 is its ceiling) and pre-netstandard2.1 runtimes (Mono <6.4, older Xamarin/Unity) can no longer reference these packages and must move to a netstandard2.1-capable target (.NET Core 3.0+/.NET 5+).
* **postgrest:** IPostgrestTable.Delete(QueryOptions?, CancellationToken) and its Table implementation now return Task<ModeledResponse> instead of Task. await table.Delete() is unaffected; code that assigns the result to Task or captures the method group must be recompiled/adjusted.

### Features

* **core:** add retry/backoff plumbing for injectable HttpClient support ([#367](https://github.com/supabase-community/supabase-csharp/issues/367)) ([eda8d8d](https://github.com/supabase-community/supabase-csharp/commit/eda8d8d3458469369b78fe2d5148e0d588acbd6e))
* **dependency-injection:** add Supabase.Extensions.DependencyInjection package ([#387](https://github.com/supabase-community/supabase-csharp/issues/387)) ([a4692ee](https://github.com/supabase-community/supabase-csharp/commit/a4692eecdb1717cbc7279eb9a5500c15e9dc87d2))
* **functions:** add retry/backoff and injectable HttpClient support ([#370](https://github.com/supabase-community/supabase-csharp/issues/370)) ([21e198a](https://github.com/supabase-community/supabase-csharp/commit/21e198afa5f21638e34476322c3867917277e01f))
* **functions:** migrate from Newtonsoft.Json to System.Text.Json ([#356](https://github.com/supabase-community/supabase-csharp/issues/356)) ([3f9780d](https://github.com/supabase-community/supabase-csharp/commit/3f9780dd331626901e297bb3ce1a88399eb86240))
* **gotrue:** add cloudflare errors code ([#398](https://github.com/supabase-community/supabase-csharp/issues/398)) ([a038165](https://github.com/supabase-community/supabase-csharp/commit/a038165076135e4316e9b71624047cd51048d0ea))
* **gotrue:** add retry/backoff and injectable HttpClient support ([#371](https://github.com/supabase-community/supabase-csharp/issues/371)) ([74a72d0](https://github.com/supabase-community/supabase-csharp/commit/74a72d0921373ad9375853ebd53bf3d2fc65c93d))
* **gotrue:** migrate from Newtonsoft.Json to System.Text.Json ([#357](https://github.com/supabase-community/supabase-csharp/issues/357)) ([0d58f45](https://github.com/supabase-community/supabase-csharp/commit/0d58f4581b7e77fd155c4460c698245dd22cb980))
* **gotrue:** support async session persistence ([#399](https://github.com/supabase-community/supabase-csharp/issues/399)) ([e0d494e](https://github.com/supabase-community/supabase-csharp/commit/e0d494e68e085db8e04d23398442eca0cb917d67))
* **gotrue:** support soft-delete on admin DeleteUser ([#402](https://github.com/supabase-community/supabase-csharp/issues/402)) ([ed9e5c4](https://github.com/supabase-community/supabase-csharp/commit/ed9e5c4fd921a78b7cf2dcb3573a87df2401af08))
* **postgrest:** add injectable HttpClient support ([#376](https://github.com/supabase-community/supabase-csharp/issues/376)) ([f6bfa69](https://github.com/supabase-community/supabase-csharp/commit/f6bfa69a19ce72eee2c631d0910aa462fffa0a50))
* **postgrest:** add opt-in retry option ([#375](https://github.com/supabase-community/supabase-csharp/issues/375)) ([07b6bfe](https://github.com/supabase-community/supabase-csharp/commit/07b6bfea86beb232bae41d1941dfbbde2780b259))
* **postgrest:** migrate from Newtonsoft.Json to System.Text.Json ([fe5364b](https://github.com/supabase-community/supabase-csharp/commit/fe5364b5598cb48dc73536a409da05162361fd11))
* **quality-gate:** mark public API shipped after release ([#403](https://github.com/supabase-community/supabase-csharp/issues/403)) ([cd77655](https://github.com/supabase-community/supabase-csharp/commit/cd77655242acfddf6ee75d18c340556171e6523d))
* **realtime:** add retry/backoff and injectable HttpClient support ([#382](https://github.com/supabase-community/supabase-csharp/issues/382)) ([252a0d0](https://github.com/supabase-community/supabase-csharp/commit/252a0d09caa1d542e03e24d305678cbc9e79cbf7))
* **realtime:** migrate from Newtonsoft.Json to System.Text.Json ([c6767dc](https://github.com/supabase-community/supabase-csharp/commit/c6767dcdf72196b8bb3e3800857d1998d8f76f4a))
* **storage:** add retry/backoff and injectable HttpClient support ([#381](https://github.com/supabase-community/supabase-csharp/issues/381)) ([b81b7f7](https://github.com/supabase-community/supabase-csharp/commit/b81b7f7763cfe127b192d3715c1b534e38a7b267))
* **storage:** expose service error code ([#380](https://github.com/supabase-community/supabase-csharp/issues/380)) ([1da90f9](https://github.com/supabase-community/supabase-csharp/commit/1da90f9ae0435d3e92138cb45d0ffb6d1452c3ba))
* **storage:** migrate from Newtonsoft.Json to System.Text.Json ([#358](https://github.com/supabase-community/supabase-csharp/issues/358)) ([673970c](https://github.com/supabase-community/supabase-csharp/commit/673970c93da740ddb9ff5dbcd4b87b13167f42ae))
* **supabase:** add retry/backoff and injectable HttpClient support ([#383](https://github.com/supabase-community/supabase-csharp/issues/383)) ([95946de](https://github.com/supabase-community/supabase-csharp/commit/95946deeeb49a39c3832cbe3b2a01362f6f139d5))
* **supabase:** migrate from Newtonsoft.Json to System.Text.Json ([#360](https://github.com/supabase-community/supabase-csharp/issues/360)) ([1915bf1](https://github.com/supabase-community/supabase-csharp/commit/1915bf182d00dc446337dee48339b9436016d027))
* **supabase:** support publishable and secret API keys ([#397](https://github.com/supabase-community/supabase-csharp/issues/397)) ([66a5fd3](https://github.com/supabase-community/supabase-csharp/commit/66a5fd3ef0daf18ff6985199670095af0405e36d))


### Bug Fixes

* **gotrue:** keep persisted sessions across restarts and network failures ([#394](https://github.com/supabase-community/supabase-csharp/issues/394)) ([ad25352](https://github.com/supabase-community/supabase-csharp/commit/ad253524aab57815f9f4fdbf9f797efa03546f23))
* **gotrue:** keep sessions the sign-in and refresh paths do not own ([#405](https://github.com/supabase-community/supabase-csharp/issues/405)) ([18c3882](https://github.com/supabase-community/supabase-csharp/commit/18c3882b7219ebed7cac62679fe67e960c2c1ab4))
* **gotrue:** resolve provider URL when linking an identity ([#391](https://github.com/supabase-community/supabase-csharp/issues/391)) ([1bb1ef5](https://github.com/supabase-community/supabase-csharp/commit/1bb1ef50a4e8ee15450e0eedec91034e6072d498))
* **gotrue:** send apikey header on standalone admin/stateless authed calls ([#401](https://github.com/supabase-community/supabase-csharp/issues/401)) ([b1a60c0](https://github.com/supabase-community/supabase-csharp/commit/b1a60c0975b83f9e422f228ca5ca77e8c1740c7c))
* **gotrue:** stop sending OAuth state to /authorize ([#388](https://github.com/supabase-community/supabase-csharp/issues/388)) ([afa5ff3](https://github.com/supabase-community/supabase-csharp/commit/afa5ff3396f7ab5e0f8b1e44690c9f98273dfc9f))
* **postgrest:** drop the '.' before nested and/or groups ([#389](https://github.com/supabase-community/supabase-csharp/issues/389)) ([f29bc5b](https://github.com/supabase-community/supabase-csharp/commit/f29bc5b2b6f9829766fef31cb82848fa9ac09805))
* **postgrest:** return deleted rows from parameterless Delete ([#342](https://github.com/supabase-community/supabase-csharp/issues/342)) ([a7ef38c](https://github.com/supabase-community/supabase-csharp/commit/a7ef38cedd849c7ad0994140b936c7b62b7644e6)), closes [#334](https://github.com/supabase-community/supabase-csharp/issues/334)
* **postgrest:** throw on multi-row Single() result instead of returning null ([#346](https://github.com/supabase-community/supabase-csharp/issues/346)) ([52985c2](https://github.com/supabase-community/supabase-csharp/commit/52985c27b56071bb6b12ba147779af033dbd9a86))
* **realtime:** send access_token in channel join frame for RLS-authorized joins ([0cb3de8](https://github.com/supabase-community/supabase-csharp/commit/0cb3de8cb6e7e3fd7b11fc252b6fb282fffbd82c))
* **realtime:** throw exception when updating postgres_changes after channel joins ([#385](https://github.com/supabase-community/supabase-csharp/issues/385)) ([190b8be](https://github.com/supabase-community/supabase-csharp/commit/190b8be46bc94e05d2171d30a289c85756dcaf40))
* **storage:** percent-encode object key in CDN purge urls ([#384](https://github.com/supabase-community/supabase-csharp/issues/384)) ([710eb59](https://github.com/supabase-community/supabase-csharp/commit/710eb59fbf994de8a9fe4106e9995b140f03346d))


### Build System

* retarget packages to netstandard2.1 ([#344](https://github.com/supabase-community/supabase-csharp/issues/344)) ([41ac485](https://github.com/supabase-community/supabase-csharp/commit/41ac485682af185c12904b451a72b28039b21eaa))

## [1.6.0](https://github.com/supabase-community/supabase-csharp/compare/v1.5.0...v1.6.0) (2026-08-07)


### Features

* bump Supabase dependencies ([#301](https://github.com/supabase-community/supabase-csharp/issues/301)) ([91c35d2](https://github.com/supabase-community/supabase-csharp/commit/91c35d23c7906f9f7662411fca99161791a87af2))


### Bug Fixes

* match auth header names case-insensitively (enables developer override) ([#295](https://github.com/supabase-community/supabase-csharp/issues/295)) ([ac057a2](https://github.com/supabase-community/supabase-csharp/commit/ac057a2d4b3abb4b7ed916e004110db79881dc9a))

## [1.5.0](https://github.com/supabase-community/supabase-csharp/compare/v1.4.0...v1.5.0) (2026-07-30)


### Features

* bump Supabase dependencies ([#293](https://github.com/supabase-community/supabase-csharp/issues/293)) ([6658bc2](https://github.com/supabase-community/supabase-csharp/commit/6658bc21c2312d0309b25be5c84722770a0c6f52))

## [1.4.0](https://github.com/supabase-community/supabase-csharp/compare/v1.3.0...v1.4.0) (2026-07-23)


### Features

* expose aggregated telemetry source names for OpenTelemetry ([#285](https://github.com/supabase-community/supabase-csharp/issues/285)) ([bc29898](https://github.com/supabase-community/supabase-csharp/commit/bc29898d89d7d98b4f2d4e31a0c64678a9672210))

## [1.3.0](https://github.com/supabase-community/supabase-csharp/compare/v1.2.0...v1.3.0) (2026-07-20)


### Features

* wire Realtime's Postgrest client automatically so models from postgres_changes support Update/Delete ([#282](https://github.com/supabase-community/supabase-csharp/issues/282)) ([584cc1c](https://github.com/supabase-community/supabase-csharp/commit/584cc1c4589d4162d73deb8b487d36cab1f96333))

## [1.2.0](https://github.com/supabase-community/supabase-csharp/compare/v1.1.2...v1.2.0) (2026-07-16)


### Features

* add dependabot ([#202](https://github.com/supabase-community/supabase-csharp/issues/202)) ([e04cc98](https://github.com/supabase-community/supabase-csharp/commit/e04cc988b45c1d8ab29bbab41a5aac3c19877e81))
* add sdk compliance file for capabilities matrix ([#267](https://github.com/supabase-community/supabase-csharp/issues/267)) ([3fad62f](https://github.com/supabase-community/supabase-csharp/commit/3fad62f9bc8edad8abec2ba5c06dd504e5e78630))
* add support for trusted publishing ([#276](https://github.com/supabase-community/supabase-csharp/issues/276)) ([5eefb2c](https://github.com/supabase-community/supabase-csharp/commit/5eefb2cb486861cc6117d69584fd4ac470abf897))


### Bug Fixes

* correct csproj filename typo in release-please config ([#274](https://github.com/supabase-community/supabase-csharp/issues/274)) ([4884c6e](https://github.com/supabase-community/supabase-csharp/commit/4884c6eef5a4a535d3c76e6ffe831e5fa8f00008))
* lower Newtonsoft.Json minimum version to 13.0.2 ([#275](https://github.com/supabase-community/supabase-csharp/issues/275)) ([d2233e2](https://github.com/supabase-community/supabase-csharp/commit/d2233e256217e59c22ef76edc93c9b682673d4c5))

## [1.1.2](https://github.com/supabase-community/supabase-csharp/compare/v1.1.1...v1.1.2) (2025-07-07)


### Bug Fixes

* 14 - Update gotrue-csharp@2.3.0 ([183a30f](https://github.com/supabase-community/supabase-csharp/commit/183a30f6c879edbe1001bb750878edc185257ccd))
* 5 ([1d30b7b](https://github.com/supabase-community/supabase-csharp/commit/1d30b7bbf953be8e9f34a2cbfee3f2257c084001))

## 1.1.1 - 2024-07-27

- Support for passing Headers specified in `ClientOptions` to `Supabase.Realtime` Client.
- Update dependency: `Supabase.Gotrue@6.0.3`
    - [Re: 105](https://github.com/supabase-community/gotrue-csharp/pull/105) Add admin calls for MFA. Big thanks
      to [@michaelschattgen](https://github.com/michaelschattgen).
- Update dependency: `Supabase.Realtime@7.0.2`
    - Updates Dependency: `Websocket.Client@5.1.2`
    - Updates Dependency: `Supabase.Postgrest@4.0.3`
    - [Re:#167](https://github.com/supabase-community/supabase-csharp/issues/167) Adds support for
      specifying `GetHeaders` on the `RealtimeClient` which are included on the initial request to the server to
      establish websocket connection.

## 1.1.0 - 2024-07-25

- Supports passing Headers specified in `ClientOptions` to child apis.
- Drop support for `netstandard2.0` - `Supabase` now targets `netstandard2.1`.
- Update dependency: `Supabase.Gotrue@6.0.2`
    - [Re: 103](https://github.com/supabase-community/gotrue-csharp/pull/103) Add support for MFA signup and login
      flows. Huge thanks to [@michaelschattgen](https://github.com/michaelschattgen) for this implementation!
    - [Re: #102](https://github.com/supabase-community/gotrue-csharp/pull/102) Add ExchangeCodeForSession to
      StatelessClient. Thanks [@alexbakker](https://github.com/alexbakker)!
    - Major: Change to targeting framework to `netstandard2.1`
        - [Re: #99](https://github.com/supabase-community/gotrue-csharp/pull/99) Use a CSPRNG to generate the code
          verifier. Thanks [@alexbakker](https://github.com/alexbakker)!
    - [Re: #101](https://github.com/supabase-community/gotrue-csharp/pull/101) Ban user functionality.
      Thanks [@celestebyte](https://github.com/celestebyte)!

## 1.0.5 - 2024-06-29

- Update dependency: `Supabase.Storage@2.0.2`
- Update dependency: `Supabase.Gotrue@5.0.6`
    - [Re: #98](https://github.com/supabase-community/gotrue-csharp/pull/98) Introduces `VerifyTokenHash` to support the
      PKCE flow for email signup. Thanks [@alexbakker](https://github.com/alexbakker)!

## 1.0.4 - 2024-06-11

- Update dependency: `Supabase.Gotrue@5.0.5`
    - Allow for scoped `SignOut`. Thanks [@AndrewKahr](https://github.com/AndrewKahr)!
    - Various minor SSO fixes. Thanks [@Rycko1](https://github.com/Rycko1)!
    - Implement `SignInWithSSO`. Huge thank you to [@Rycko1](https://github.com/Rycko1)!
- Update dependency `Supabase.Postgrest@4.0.3`
    - Re: [#97](https://github.com/supabase-community/postgrest-csharp/pull/97) Fix set null value on string property.
      Thanks [@alustrement-bob](https://github.com/alustrement-bob)!

## 1.0.3 - 2024-05-22

- Update dependency: `Supabase.Gotrue@5.0.2`
    - Add missing properties (`ProviderRefreshToken` and `ProviderToken`) to `Session` object to reflect current state
      of `auth-js`
- Update dependency: `Supabase.Realtime@7.0.1`
    - Re: [#47](https://github.com/supabase-community/realtime-csharp/issues/47) Return a Task from `Track`
      and `Untrack`
      methods

## 1.0.2 - 2024-05-16

- Update dependency: `Supabase.Postgrest@4.0.2`
    - Re: [#96](https://github.com/supabase-community/postgrest-csharp/pull/96) Set `ConfigureAwait(false)` the response
      to
      prevent deadlocking applications. Thanks [@pur3extreme](https://github.com/pur3extreme)!
- Update dependency: `Supabase.Gotrue@5.0.1`
    - Re: [#96](https://github.com/supabase-community/postgrest-csharp/pull/96) Set `ConfigureAwait(false)` the response
      to
      prevent deadlocking applications. Thanks [@pur3extreme](https://github.com/pur3extreme)!
- Update dependency: `Supabase.Storage@2.0.1`
    - Re: [#15](https://github.com/supabase-community/storage-csharp/issues/15)
      and [#16](https://github.com/supabase-community/storage-csharp/pull/16)
      Fix CreateSignedUrl with TransformOptions. Thanks [@alustrement-bob](https://github.com/alustrement-bob)!

## 1.0.1 - 2024-05-07

- Update dependency: `Supabase.Postgrest@4.0.1`
    - Re: [#92](https://github.com/supabase-community/postgrest-csharp/issues/92) Changes `IPostgrestTable<>` contract
      to return the interface rather than a concrete type.

## 1.0.0 - 2024-04-21

- Assembly Name has been changed to `Supabase.dll`
- Update dependency: `Supabase.Postgrest@4.0.0`
    - [MAJOR] Moves namespaces from `Postgrest` to `Supabase.Postgrest`
    - Re: [#135](https://github.com/supabase-community/supabase-csharp/issues/135) Update nuget package
      name `postgrest-csharp` to `Supabase.Postgrest`
- Update dependency: `Supabase.Gotrue@5.0.0`
    - Re: [#135](supabase-community/supabase-csharp#135) Update nuget package name `gotrue-csharp` to `Supabase.Gotrue`
    - Re: [#89](https://github.com/supabase-community/gotrue-csharp/issues/89), Only add `access_token` to request body
      when it is explicitly declared.
    - [MINOR] Re: [#89](https://github.com/supabase-community/gotrue-csharp/issues/89) Update signature
      for `SignInWithIdToken` which adds an optional `accessToken` parameter, update doc comments, and
      call `DestroySession` in method
    - Re: [#88](https://github.com/supabase-community/gotrue-csharp/issues/88), Add `IsAnonymous` property to `User`
    - Re: [#90](https://github.com/supabase-community/gotrue-csharp/issues/90) Implement `LinkIdentity`
      and `UnlinkIdentity`
- Update dependency: `Supabase.Realtime@7.0.0`
    - Merges [#45](https://github.com/supabase-community/realtime-csharp/pull/45) - Updating
      the `Websocket.Client@5.1.1`
    - Re: [#135](https://github.com/supabase-community/supabase-csharp/issues/135) Update nuget package
      name `realtime-csharp` to `Supabase.Realtime`
- Update dependency: `Supabase.Storage@2.0.0`
    - Re: [#135](https://github.com/supabase-community/supabase-csharp/issues/135) Update nuget package
      name `storage-csharp` to `Supabase.Storage`
- Update dependency: `Supabase.Functions@2.0.0`
    - Re: [#135](https://github.com/supabase-community/supabase-csharp/issues/135) Update nuget package
      name `functions-csharp` to `Supabase.Functions`
- Update dependency: `Supabase.Core@1.0.0`
    - Re: [#135](https://github.com/supabase-community/supabase-csharp/issues/135) Update nuget package
      name `supabase-core`
      to `Supabase.Core`
- Adds comments to the remaining undocumented code.

## 0.16.2 - 2024-04-02

- Update dependency: `gotrue-csharp@4.2.7`
    - [#88](https://github.com/supabase-community/gotrue-csharp/issues/88) Implement `signInAnonymously` from the JS
      client
    - Include additional 3rd party providers in constants.

## 0.16.1 - 2024-03-15

- Update dependency: `postgrest-csharp@3.5.1`
    - Re: [#147](https://github.com/supabase-community/supabase-csharp/issues/147) - Supports `Rpc` specifying a generic
      type for its return.

## 0.16.0 - 2024-03-12

- Update dependency: `postgrest-csharp@3.5.0`
    - Re: [#78](https://github.com/supabase-community/postgrest-csharp/issues/78), Generalize query filtering creation
      in `Table` so that it matches new generic signatures.
    - Move from `QueryFilter` parameters to a more generic `IPosgrestQueryFilter` to support constructing new
      QueryFilters from a LINQ expression.
        - Note: Lists of `QueryFilter`s will now need to be defined
          as: `new List<IPostgrestQueryFilter> { new QueryFilter(), ... }`
    - Adjust serialization of timestamps within a `QueryFilter` to support `DateTime` and `DateTimeOffset` using the
      ISO-8601 (https://stackoverflow.com/a/115002)
- Update dependency: `functions-csharp@1.3.2`
    - Re: [#5](https://github.com/supabase-community/functions-csharp/issues/5) Add support for specifying Http Timeout
      on a function call by adding `HttpTimeout` to `InvokeFunctionOptions`

## 0.15.0 - 2024-01-08

- Update Dependency: `gotrue-csharp@4.2.6`
    - [#83](https://github.com/supabase-community/gotrue-csharp/pull/83) Replaces JWTDecoder package with
      System.IdentityModel.Tokens.Jwt. Thanks [@FantasyTeddy](https://github.com/FantasyTeddy)!
- Update Dependency: `postgrest-csharp@3.4.1`
    - Re: [#85](https://github.com/supabase-community/postgrest-csharp/issues/85) Fixes problem when using multiple
      .Order()
      methods by merging [#86](https://github.com/supabase-community/postgrest-csharp/pull/86).
      Thanks [@hunsra](https://github.com/hunsra)!
    - Re: [#81](https://github.com/supabase-community/postgrest-csharp/issues/81)
        - [Minor] Removes `IgnoreOnInsert`and `IgnoreOnUpdate` from `ReferenceAttribute` as changing these properties
          to `false` does not currently provide the expected functionality.
        - Fixes `Insert` and `Update` not working on models that have `Reference` specified on a property with a
          non-null
          value.

## 0.14.0 - 2023-12-15

- Update Dependency: `gotrue-csharp@4.2.5`
    - [#82](https://github.com/supabase-community/gotrue-csharp/issues/81) - Implements #82 - Creates a `GenerateLink`
      method on the `AdminClient` that supports `signup`, `invite`, `magiclink`, `recovery`, `email_change_new`
      and `email_change_current`
    - [#81](https://github.com/supabase-community/gotrue-csharp/issues/81) - Adds `InviteUserByEmailOptions` as a
      parameter to the Gotrue Admin Client
- Update Dependency: `postgrest-csharp@3.3.0`
    - Re: [#78](https://github.com/supabase-community/postgrest-csharp/issues/78) Updates signatures for `Not`
      and `Filter` to include generic types for a better development experience.
    - Updates internal generic type names to be more descriptive.
    - Add support for LINQ predicates on `Table<TModel>.Not()` signatures

## 0.13.7 - 2023-11-13

- Update Dependency: `postgrest-csharp@3.2.10`
    - Re: [#76](https://github.com/supabase-community/postgrest-csharp/issues/76) Removes the
      incorrect `ToUniversalTime` conversion in the LINQ `Where` parser.

## 0.13.6 - 2023-10-12

- Update Dependency: `gotrue-csharp@4.2.3`
    - Re: [#80](https://github.com/supabase-community/gotrue-csharp/pull/80) Fixes `Session.Expires()` not being
      calculated correctly. Thanks [@dayjay](https://github.com/Dayjay)!

## 0.13.5 - 2023-10-09

- Update Dependency: `postgrest-csharp@3.2.9`
    - Re: [supabase-csharp#115](https://github.com/supabase-community/supabase-csharp/discussions/115) Additional
      support for a model referencing another model with multiple foreign keys.
    - Re: [supabase-csharp#115](https://github.com/supabase-community/supabase-csharp/discussions/115) Adds support for
      multiple references attached to the same model (foreign keys) on a single C# Model.

## 0.13.4 - 2023-10-08

- Update Dependency: `gotrue-csharp@4.2.2`
    - Re: [#78](https://github.com/supabase-community/gotrue-csharp/issues/78) - Implements PKCE flow support
      for `ResetPasswordForEmail`.

## 0.13.3 - 2023-09-15

- Re: [#107](https://github.com/supabase-community/supabase-csharp/issues/107) - removes Realtime socket being
  disconnected on a User sign-out - only the subscriptions should be removed.

## 0.13.2 - 2023-09-15

- Update dependency: `postgrest-csharp@3.2.7`
    - Implements a `TableWithCache` for `Get` requests that can pull reactive Models from cache before making a remote
      request.
    - Re: [supabase-csharp#85](https://github.com/supabase-community/supabase-csharp/issues/85) Includes sourcelink
      support.
    - Re: [#75](https://github.com/supabase-community/postgrest-csharp/pull/75) Fix issue with marshalling of stored
      procedure arguments. Big thank you to [@corrideat](https://github.com/corrideat)!

## 0.13.1 - 2023-08-26

- Update dependency: `supabase-storage-csharp@1.4.0`
    - Fixes [#11](https://github.com/supabase-community/storage-csharp/issues/11) - Which implements
      missing `SupabaseStorageException` on failure status codes for `Upload`, `Download`, `Move`, `CreateSignedUrl`
      and `CreateSignedUrls`.

## 0.13.0 - 2023-08-26

- Update dependency: `gotrue-csharp@4.2.1`
    - [#74](https://github.com/supabase-community/gotrue-csharp/pull/74) - Fixes bug where token refresh interval was
      not honored by client. Thanks [@slater1](https://github.com/slater1)!
    - **Minor Breaking changes:** [#72](https://github.com/supabase-community/gotrue-csharp/pull/72) - Fixes
      Calling `SetAuth` does not actually set Authorization Headers for subsequent requests by implementing `SetSession`
        - Removes `RefreshToken(string refreshToken)` and `SetAuth(string accessToken` in favor
          of `SetSession(string accessToken, string refreshToken)`
        - Makes `RefreshAccessToken` require `accessToken` and `refreshToken` as parameters - overrides the
          authorization
          headers to use the supplied token
        - Migrates project internal times to use `DateTime.UtcNow` over `DateTime.Now`.

## 0.12.2 - 2023-07-28

- Update dependency: `realtime-csharp@6.0.4`
    - Fixes [#29](https://github.com/supabase-community/realtime-csharp/issues/29) Where the Realtime client could
      disconnect from channels after a few hours and fail to reconnect by removing the case where the `IsSubscribe` flag
      is flipped when encountering a channel error.
- Update dependency: `postgrest-csharp@3.2.5`
    - Re: [supabase-community/supabase-csharp#81](https://github.com/supabase-community/supabase-csharp/discussions/81):
      Clarifies `ReferenceAttribute` by changing `shouldFilterTopLevel` to `useInnerJoin` and adds an additional
      constructor for `ReferenceAttribute` with a shortcut for specifying the `JoinType`

## 0.12.1 - 2023-06-29

- Update dependency: `gotrue-csharp@4.1.1`
    - [#68](https://github.com/supabase-community/gotrue-csharp/pull/68) Changes Network Status to use the interface
      instead of client
- Update dependency: `postgrest-csharp@3.2.4`
    - [#70](https://github.com/supabase-community/postgrest-csharp/pull/70) Minor Unity related fixes

## 0.12.0 - 2023-06-25

- Update dependency: `gotrue-csharp@4.1.0`
    - **Minor** [#66](https://github.com/supabase-community/gotrue-csharp/pull/66) - Separates out Admin JWT
      functionality into a separate `AdminClient`
    - [#67](https://github.com/supabase-community/gotrue-csharp/pull/67) - Adds shutdown method which terminates the
      background refresh threads.
    - Movement of much of the documentation for methods out of their classes and into their interfaces.
    - Language features locked to C#9
- Update dependency: `postgrest-csharp@3.2.3`
    - [#69](https://github.com/supabase-community/postgrest-csharp/pull/69) Locks language version to C#9
    - [#68](https://github.com/supabase-community/postgrest-csharp/pull/68) Makes RPC parameters optional

Thanks [@wiverson](https://github.com/wiverson) for the work in this release!

## 0.11.1 - 2023-06-10

- Update dependencies: `functions-csharp@1.3.1`, `gotrue-csharp@4.0.4`, `postgrest-csharp@3.2.2`,
  `realtime-csharp@6.0.3`, `supabase-storage-csharp@1.3.2`, `supabase-core@0.0.3`
    - Namespaces assembly names to make them unique among other dependencies, i.e: `Core.dll`
      becomes `Supabase.Core.dll` which will hopefully prevent future collisions.

## 0.11.0 - 2023-05-24

- Update dependency: postgrest-csharp@3.2.0
    - General codebase and QOL improvements. Exceptions are generally thrown through `PostgrestException` now instead
      of `Exception`. A `FailureHint.Reason` is provided with failures if possible to parse.
    - `AddDebugListener` is now available on the client to help with debugging
    - Merges [#65](https://github.com/supabase-community/postgrest-csharp/pull/65) Cleanup + Add better exception
      handling
    - Merges [#66](https://github.com/supabase-community/postgrest-csharp/pull/66) Local test Fixes
    - Fixes [#67](https://github.com/supabase-community/postgrest-csharp/issues/67) Postgrest Reference attribute is
      producing StackOverflow for circular references
- Update dependency: gotrue-csharp@4.0.2
    - [#58](https://github.com/supabase-community/gotrue-csharp/issues/58) - Add support for the `reauthentication`
      endpoint which allows for secure password changes.
- Update dependency: realtime-csharp@6.0.1
    - Updates publishing action for future packages, includes README and icon.
    - Merges [#28](https://github.com/supabase-community/realtime-csharp/pull/28)
      and [#30](https://github.com/supabase-community/realtime-csharp/pull/30)
    - The realtime client now takes a "fail-fast" approach. On establishing an initial connection, client will throw
      a `RealtimeException` in `ConnectAsync()` if the socket server is unreachable. After an initial connection has
      been
      established, the **client will continue attempting reconnections indefinitely until disconnected.**
    - [Major, New] C# `EventHandlers` have been changed to `delegates`. This should allow for cleaner event data access
      over
      the previous subclassed `EventArgs` setup. Events are scoped accordingly. For example, the `RealtimeSocket` error
      handlers will receive events regarding socket connectivity; whereas the `RealtimeChannel` error handlers will
      receive
      events according to `Channel` joining/leaving/etc. This is implemented with the following methods prefixed by (
      Add/Remove/Clear):
        - `RealtimeBroadcast.AddBroadcastEventHandler`
        - `RealtimePresence.AddPresenceEventHandler`
        - `RealtimeSocket.AddStateChangedHandler`
        - `RealtimeSocket.AddMessageReceivedHandler`
        - `RealtimeSocket.AddHeartbeatHandler`
        - `RealtimeSocket.AddErrorHandler`
        - `RealtimeClient.AddDebugHandler`
        - `RealtimeClient.AddStateChangedHandler`
        - `RealtimeChannel.AddPostgresChangeHandler`
        - `RealtimeChannel.AddMessageReceivedHandler`
        - `RealtimeChannel.AddErrorHandler`
        - `Push.AddMessageReceivedHandler`
    - [Major, new] `ClientOptions.Logger` has been removed in favor of `Client.AddDebugHandler()` which allows for
      implementing custom logging solutions if desired.
        - A simple logger can be set up with the following:
      ```c#
      client.AddDebugHandler((sender, message, exception) => Debug.WriteLine(message));
      ```
    - [Major] `Connect()` has been marked `Obsolete` in favor of `ConnectAsync()`
    - Custom reconnection logic has been removed in favor of using the built-in logic from `Websocket.Client@4.6.1`.
    - Exceptions that are handled within this library have been marked as `RealtimeException`s.
    - The local, docker-composed test suite has been brought back (as opposed to remotely testing on live supabase
      servers)
      to test against.
    - Comments have been added throughout the entire codebase and an `XML` file is now generated on build.

## 0.10.0 - 2023-05-14

- Changes options to require `Supabase.SupabaseOptions.SessionPersistor` from using `ISupabaseSessionHandler`
  to `IGotrueSessionPersistance<Session>` (these are now synchronous operations).
- Update dependency: gotrue-csharp@4.0.1
    - [#60](https://github.com/supabase-community/gotrue-csharp/pull/60) - Add interfaces, bug fixes, additional error
      reason detection. Thanks [@wiverson](https://github.com/wiverson)!
    - [#57](https://github.com/supabase-community/gotrue-csharp/pull/57) Refactor exceptions, code cleanup, and move to
      delegate auth state changes
        - Huge thank you to [@wiverson](https://github.com/wiverson) for his help on this refactor and release!
        - Changes
            - Exceptions have been simplified to a single `GotrueException`. A `Reason` field has been added
              to `GotrueException` to clarify what happened. This should also be easier to manage as the Gotrue
              server API & messages evolve.
            - The session delegates for `Save`/`Load`/`Destroy` have been simplified to no longer require `async`.
            - Console logging in a few places (most notable the background refresh thread) has been removed
              in favor of a notification method. See `Client.AddDebugListener()` and the test cases for examples.
              This will allow you to implement your own logging strategy (write to temp file, console, user visible
              err console, etc).
            - The client now more reliably emits AuthState changes.
            - There is now a single source of truth for headers in the stateful Client - the `Options` headers.
        - New feature:
            - Added a `Settings` request to the stateless API only - you can now query the server instance to
              determine if it's got the settings you need. This might allow for things like a visual
              component in a tool to verify the GoTrue settings are working correctly, or tests that run differently
              depending on the server configuration.
        - Implementation notes:
            - Test cases have been added to help ensure reliability of auth state change notifications
              and persistence.
            - Persistence is now managed via the same notifications as auth state change

## 0.9.1 - 2023-04-28

- Update dependency: gotrue-csharp@3.1.1
    - Implements `SignInWithIdToken` for Apple/Google signing from LW7. A HUGE thank you
      to [@wiverson](https://github.com/wiverson)!
- Update dependency: realtime-csharp@5.0.5
    - Re: [#27](https://github.com/supabase-community/realtime-csharp/issues/27) `PostgresChangesOptions` was not
      setting `listenType` in constructor. Thanks [@Kuffs2205](https://github.com/Kuffs2205)
- Update dependency: supabase-storage-csharp@1.2.10
    - Re: [#7](https://github.com/supabase-community/storage-csharp/issues/7) Implements a `DownloadPublicFile` method.

## 0.9.0 - 2023-04-12

- Update dependency: gotrue-csharp@3.1.0
    - [Minor] Implements PKCE auth flow. SignIn using a provider now returns an instance of `ProviderAuthState` rather
      than a `string`.

- Update dependency: supabase-storage-csharp@1.2.9
    - Implements storage features from LW7:
        - feat: custom file size limit and mime types at bucket
          level [supabase/storage-js#151](https://github.com/supabase/storage-js/pull/151) file size and mime type
          limits per bucket
        - feat: quality option, image
          transformation [supabase/storage-js#145](https://github.com/supabase/storage-js/pull/152) quality option for
          image transformations
        - feat: format option for webp
          support [supabase/storage-js#142](https://github.com/supabase/storage-js/pull/142) format option for image
          transformation

## 0.8.8 - 2023-03-29

- Update dependency: gotrue-csharp@3.0.6
    - Supports adding `SignInOptions` (i.e. `RedirectTo`) on `OAuth Provider` SignIn requests.

## 0.8.7 - 2023-03-23

- Update dependency: realtime-csharp@5.0.4
    - Re: [#26](https://github.com/supabase-community/realtime-csharp/pull/26) - Fixes Connect() not returning callback
      result when the socket isn't null. Thanks [@BlueWaterCrystal](https://github.com/BlueWaterCrystal)!

## 0.8.6 - 2023-03-23

- Update dependency: supabase-storage-csharp@1.2.8
    - [Merge #5](https://github.com/supabase-community/storage-csharp/pull/5) Added search string as an optional search
      parameter. Thanks [@ElectroKnight22](https://github.com/ElectroKnight22)!

## 0.8.5 - 2023-03-10

- Update dependency: realtime-csharp@5.0.3
    - Re: [#25](https://github.com/supabase-community/realtime-csharp/issues/25) - Support Channel being resubscribed
      after having been unsubscribed, fixes rejoin timer being erroneously called on channel `Unsubscribe`.
      Thanks [@Kuffs2205](https://github.com/Kuffs2205)!

## 0.8.4 - 2023-03-03

- Update dependency: supabase-storage-csharp@1.2.7
    - Re: [#4](https://github.com/supabase-community/storage-csharp/issues/4) Implementation for `ClientOptions` which
      supports specifying Upload, Download, and Request timeouts.
- Update dependency: realtime-csharp@5.0.2
    - Re: [#24](https://github.com/supabase-community/realtime-csharp/issues/24) - Fixes join failing until reconnect
      happened + adds access token push on channel join. Big thank you to [@Honeyhead](https://github.com/honeyhead) for
      the help debugging and identifying!

## 0.8.3 - 2023-02-26

- Update dependency: supabase-storage-csharp@1.2.5
    - Provides fix
      for [supabase-community/supabase-csharp#54](https://github.com/supabase-community/supabase-csharp/issues/54) -
      Dynamic headers were always being overwritten by initialized token headers, so the storage client would not
      receive user's access token as expected.
    - Provides fix for upload progress not reporting
      in [supabase-community/storage-csharp#3](https://github.com/supabase-community/storage-csharp/issues/3)
- Update dependency: gotrue-csharp@3.0.5
    - Fixes [#44](https://github.com/supabase-community/gotrue-csharp/issues/44) - refresh timer should automatically
      reattempt (interval of 5s) for HTTP exceptions - gracefully exits on invalid refresh and triggers
      an `AuthState.Changed` event

## 0.8.2 - 2023-02-26

- Update dependency: supabase-storage-csharp@1.2.4
    - `UploadOrUpdate` now appropriately throws request exceptions

## 0.8.1 - 2023-02-06

- Update dependency: realtime-csharp@5.0.1
    - Re: [#22](https://github.com/supabase-community/realtime-csharp/issues/22) - `SerializerSettings` were not being
      passed to `PostgresChangesResponse` - Thanks [@Shenrak](https://github.com/Shenrak) for the help debugging!

## 0.8.0 - 2023-01-31

- Update dependency: realtime-csharp@5.0.0
    - Re: [#21](https://github.com/supabase-community/realtime-csharp/pull/21) Provide API for `presence`, `broadcast`
      and `postgres_changes`
        - [Major, New] `Channel.PostgresChanges` event will receive the wildcard `*` changes event,
          not `Channel.OnMessage`.
        - [Major] `Channel.OnInsert`, `Channel.OnUpdate`, and `Channel.OnDelete` now conform to the server's payload
          of `Response.Payload.**Data**`
        - [Major] `Channel.OnInsert`, `Channel.OnUpdate`, and `Channel.OnDelete` now return `PostgresChangesEventArgs`
        - [Minor] Rename `Channel` to `RealtimeChannel`
        - Supports better handling of disconnects in `RealtimeSocket` and adds a `Client.OnReconnect` event.
        - [Minor] Moves `ChannelOptions` to `Channel.ChannelOptions`
        - [Minor] Moves `ChannelStateChangedEventArgs` to `Channel.ChannelStateChangedEventArgs`
        - [Minor] Moves `Push` to `Channel.Push`
        - [Minor] Moves `Channel.ChannelState` to `Constants.ChannelState`
        - [Minor] Moves `SocketResponse`, `SocketRequest`, `SocketResponsePayload`, `SocketResponseEventArgs`,
          and `SocketStateChangedEventArgs` to `Socket` namespace.
        - [New] Adds `RealtimeBroadcast`
        - [New] Adds `RealtimePresence`
        - [Improvement] Better handling of disconnection/reconnection
- Update dependency: postgrest-csharp@3.1.3
    - Another fix for [#61](https://github.com/supabase-community/postgrest-csharp/issues/61) which futher typechecks
      nullable values.

## 0.7.2 - 2023-01-27

- Update dependency: gotrue-csharp@3.0.4
    - Makes `Session.CreatedAt` a publicly settable property, which should fix incorrect dates on retrieved `Session`s.
- Update dependency: postgrest-csharp@3.1.2
    - Fix [#61](https://github.com/supabase-community/postgrest-csharp/issues/61) which did not correctly parse
      Linq `Where` when encountering a nullable type.
    - Add missing support for transforming for `== null` and `!= null`

## 0.7.1 - 2023-01-17

- Update dependency: postgrest-csharp@3.1.1
    - Fix issue from supabase-community/supabase-csharp#48 where boolean model properties would not be evaluated in
      predicate expressions

## 0.7.0 - 2023-01-16

- Update dependency: postgrest-csharp@3.1.0
    - [Minor] Breaking API Change: `PrimaryKey` attribute defaults to `shouldInsert: false` as most uses will have the
      Database generate the primary key.
    - Merged [#60](https://github.com/supabase-community/postgrest-csharp/pull/60) which Added linq support
      for `Select`, `Where`, `OnConflict`, `Columns`, `Order`, `Update`, `Set`, and `Delete`

## 0.6.2 - 2022-11-22

- Update dependency: postgrest-csharp@3.0.4
    - `GetHeaders` is now passed to `ModeledResponse` and `BaseModel` so that the default `Update` and `Delete` methods
      use the latest credentials
    - `GetHeaders` is used in `Rpc` calls (re: [#39](https://github.com/supabase-community/supabase-csharp/issues/39))

## 0.6.1 - 2022-11-12

- [Hotfix] `GetHeaders` was not passing properly to `SupabaseTable` and `Gotrue.Api`

## 0.6.0 - 2022-11-12

[BREAKING CHANGES]

- `Client` is no longer a singleton, singleton interactions (if desired) are left to the developer to implement.
- `Client` supports injection of dependent clients after initialization via property:
    - `Auth`
    - `Functions`
    - `Realtime`
    - `Postgrest`
    - `Storage`
- `SupabaseModel` contains no logic but remains for backwards compatibility. (Marked `Obsolete`)
- `ClientOptions.ShouldInitializeRealtime` was removed (no longer auto initialized)
- `ClientOptions` now references an `ISupabaseSessionHandler` which specifies expected functionality for session
  persistence on Gotrue (replaces `ClientOptions.SessionPersistor`, `ClientOptions.SessionRetriever`,
  and `ClientOptions.SessionDestroyer`).
- `supabase-csharp` and all child libraries now have support `nullity`

Other Changes:

- Update dependency: functions-csharp@1.2.1
- Update dependency: gotrue-csharp@3.0.2
- Update dependency: postgrest-csharp@3.0.2
- Update dependency: realtime-csharp@4.0.1
- Update dependency: supabase-storage-csharp@1.2.3
- Update dependency: supabase-core@0.0.2

Big thank you to [@veleek](https://github.com/veleek) for his insight into these changes.

Re: [#35](https://github.com/supabase-community/supabase-csharp/issues/35), [#34](https://github.com/supabase-community/supabase-csharp/issues/34), [#23](https://github.com/supabase-community/supabase-csharp/issues/23), [#36](https://github.com/supabase-community/supabase-csharp/pull/36)

## 0.5.3 - 2022-10-11

- Update dependency: postgrest-csharp@2.1.0
    - [Minor] Breaking API change: Remove `BaseModel.PrimaryKeyValue` and `BaseModel.PrimaryKeyColumn` in favor of
      a `PrimaryKey` dictionary with support for composite keys.
    - Re: [#48](https://github.com/supabase-community/postgrest-csharp/issues/48) - Add support for derived models
      on `ReferenceAttribute`
    - Re: [#49](https://github.com/supabase-community/postgrest-csharp/issues/49) - Added `Match(T model)`

## 0.5.2 - 2022-9-13

- Update dependency: postgrest-csharp@2.0.12
    - Merged [#47](https://github.com/supabase-community/postgrest-csharp/pull/49) which added cancellation token
      support to `Table<T>` methods. Thanks [@devpikachu](https://github.com/devpikachu)!

## 0.5.1 - 2022-8-1

- Update dependency: postgrest-csharp@2.0.11
- Update dependency: supabase-storage-csharp@1.1.1

## 0.5.0 - 2022-7-17

- Update dependency: postgrest-csharp@2.0.9
- Update dependency: realtime-csharp@3.0.1
- Update dependency: supabase-storage-csharp@1.1.0
    - API Change [Breaking/Minor] Library no longer uses `WebClient` and instead leverages `HttpClient`. Progress events
      on `Upload` and `Download` are now handled with `EventHandler<float>` instead of `WebClient` EventHandlers.

## 0.4.4 - 2022-5-24

- Update dependency: gotrue-csharp@2.4.5
- Update dependency: postgrest-csharp@2.0.8

## 0.4.3 - 2022-5-13

- Update dependency: gotrue-csharp@2.4.4

## 0.4.2 - 2022-4-30

- Update dependency: gotrue-csharp@2.4.3

## 0.4.1 - 2022-4-23

- Update dependency: gotrue-csharp@2.4.2

## 0.4.0 - 2022-4-12

- Add support for functions-csharp@1.0.1, giving access to invoking Supabase's edge functions.
- Update dependency: gotrue-csharp@2.4.1

## 0.3.5 - 2022-4-11

- Update dependency: postgres-csharp@2.0.7

## 0.3.4 - 2022-03-28

- Update dependency: gotrue-csharp@2.4.0

## 0.3.3 - 2022-02-27

- Update dependency: gotrue-csharp@2.3.6
- Update dependency: supabase-storage-csharp@1.0.2

## 0.3.2 - 2022-02-18

- Update dependency: realtime-csharp@3.0.0
    - Exchange existing websocket client: [WebSocketSharp](https://github.com/sta/websocket-sharp)
      for [Marfusios/websocket-client](https://github.com/Marfusios/websocket-client) which adds support for Blazor WASM
      apps.
      Ref: [#14](https://github.com/supabase-community/realtime-csharp/pull/14)

## 0.3.1 - 2022-01-20

- Update dependency: gotrue-csharp@2.3.5
    - [#23](https://github.com/supabase-community/gotrue-csharp/pull/23) Added `redirect_url` option for MagicLink sign
      in (Thanks [@MisterJimson](https://github.com/MisterJimson))
    - [#21](https://github.com/supabase-community/gotrue-csharp/pull/21) Added SignOut method to Stateless Client (
      Thanks [@fplaras](https://github.com/fplaras))

## 0.3.0 - 2021-12-30

- Update dependency: postgrest-csharp@2.0.6
    - Add support for `NullValueHandling` to be specified on a `Column` Attribute and for it to be honored on Inserts
      and Updates. Defaults to: `NullValueHandling.Include`.
        - Implements [#38](https://github.com/supabase-community/postgrest-csharp/issues/38)
- Update dependency: realtime-csharp@2.0.8
    - Implement Upstream Realtime RLS Error Broadcast Handler
        - Implements [#12](https://github.com/supabase-community/realtime-csharp/issues/12)
    - `SocketResponse` now exposes a method: `OldModel`, that hydrates the `OldRecord` property into a model.

## 0.2.12 - 2021-12-29

- Update dependency: gotrue-csharp@2.3.3
    - `SignUp` will return a `Session` with a *populated `User` object* on an unconfirmed signup.
        - Fixes [#19](https://github.com/supabase-community/gotrue-csharp/issues/19)
        - Developers who were using a `null` check on `Session.User` will need to adjust accordingly.
- Update dependency: postgrest-csharp@2.0.5
    - Fix for [#37](https://github.com/supabase-community/postgrest-csharp/issues/37) - Return Type `minimal` would fail
      to resolve because of incorrect `Accept` headers. Added header and test to verify for future.
    - Fix for [#36](https://github.com/supabase-community/postgrest-csharp/issues/36) - Inserting/Upserting bulk records
      would fail while doing an unnecessary generic coercion.

## 0.2.11 - 2021-12-24

- Update dependency: gotrue-csharp@2.3.2 (changes CreateUser parameters to conform to `AdminUserAttributes`)
    - See [#15](https://github.com/supabase-community/supabase-csharp/issues/15)
    - See [#16](https://github.com/supabase-community/supabase-csharp/issues/16)
- Update dependency: realtime-csharp@2.0.7
    - See [#13](https://github.com/supabase-community/supabase-csharp/issues/13)

## 0.2.10 - 2021-12-23

- Update dependency: gotrue-csharp@2.3.0 (adds metadata support for user signup,
  see [#14](https://github.com/supabase/community/issues/14))

## 0.2.9 - 2021-12-9

- Separate Storage client from Supabase repo and into `storage-csharp`, `supabase-csharp` now references new repo.

## 0.2.8 - 2021-12-4

- Update gotrue-csharp to 2.2.4
    - Adds support for `ListUsers` (paginate, sort, filter), `GetUserById`, `CreateUser`, and `UpdateById`

## 0.2.7 - 2021-12-2

- Update gotrue-csharp to 2.2.3
    - Adds support for sending password resets to users.

## 0.2.6 - 2021-11-29

- Support for [#12](https://github.com/supabase-community/supabase-csharp/issues/12)
- Update realtime-csharp to 2.0.6
- Update gotrue-csharp to 2.2.2
- Add `StatelessClient` re:[#7](https://github.com/supabase-community/supabase-csharp/issues/7)
