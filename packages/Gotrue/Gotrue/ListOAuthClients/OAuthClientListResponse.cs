using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.ListOAuthClients;

internal sealed record OAuthClientListResponse
{
    [JsonPropertyName("clients")]
    public IReadOnlyList<OAuthClientResponse>? Clients { get; init; }

    internal static IReadOnlyList<OAuthClient> Read(OAuthClientListResponse? response) =>
        (response?.Clients ?? Array.Empty<OAuthClientResponse>()).Select(client => client.ToOAuthClient()).ToList();
}
