# Supabase.Core

[![Build and Test](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/supabase/supabase-csharp/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Supabase.Core)](https://www.nuget.org/packages/Supabase.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../../LICENSE)

Shared primitives for the [Supabase C# SDK](https://github.com/supabase/supabase-csharp) —
common helpers, extensions, attributes, and diagnostics used across the `Supabase.*` packages.

This package is an internal building block. You normally get it transitively by installing
[`Supabase`](../Supabase/README.md) or any individual service package (Auth, Postgrest, Storage,
Realtime, Functions), and rarely reference it directly.

## Installation

```sh
dotnet add package Supabase.Core
```

Targets .NET Standard 2.0.

## Contributing

Contributions are welcome. See the [repository root](https://github.com/supabase/supabase-csharp)
for how to build and test the SDK.

## License

[MIT](../../LICENSE)
