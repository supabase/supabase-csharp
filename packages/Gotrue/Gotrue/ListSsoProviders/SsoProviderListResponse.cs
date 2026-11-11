using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.ListSsoProviders;

internal sealed record SsoProviderListResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<SsoProviderResponse>? Items { get; init; }

    internal static IReadOnlyList<SsoProvider> Read(SsoProviderListResponse? response) =>
        (response?.Items ?? Array.Empty<SsoProviderResponse>()).Select(provider => provider.ToSsoProvider()).ToList();
}
