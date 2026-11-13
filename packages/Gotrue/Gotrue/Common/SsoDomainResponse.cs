using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Common;

internal sealed record SsoDomainResponse
{
    [JsonPropertyName("domain")]
    public string Domain { get; init; } = null!;
}
