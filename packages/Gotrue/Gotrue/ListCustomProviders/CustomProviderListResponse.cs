using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.ListCustomProviders;

internal sealed record CustomProviderListResponse
{
    [JsonPropertyName("providers")]
    public IReadOnlyList<CustomProviderResponse>? Providers { get; init; }

    internal static IReadOnlyList<CustomProvider> Read(CustomProviderListResponse? response) =>
        (response?.Providers ?? Array.Empty<CustomProviderResponse>()).Select(provider => provider.ToCustomProvider()).ToList();
}
