using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.GetBucketLifecycle;

internal sealed class GetBucketLifecycleResponse
{
    [JsonRequired]
    [JsonPropertyName("rules")]
    public IReadOnlyList<RuleBody> Rules { get; init; } = null!;

    internal BucketLifecycle ToLifecycle() => new() { Rules = this.Rules.Select(rule => rule.ToRule()).ToList() };

    internal sealed class RuleBody
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonRequired]
        [JsonPropertyName("status")]
        public string Status { get; init; } = null!;

        [JsonPropertyName("noncurrentVersionExpiration")]
        public ExpirationBody? NoncurrentVersionExpiration { get; init; }

        internal LifecycleRule ToRule() => new()
        {
            Id = this.Id,
            Status = LifecycleRuleStatus.FromWire(this.Status),
            NoncurrentVersionExpiration = this.NoncurrentVersionExpiration?.ToExpiration(),
        };
    }

    internal sealed class ExpirationBody
    {
        [JsonRequired]
        [JsonPropertyName("noncurrentDays")]
        public int NoncurrentDays { get; init; }

        [JsonPropertyName("newerNoncurrentVersions")]
        public int? NewerNoncurrentVersions { get; init; }

        internal NoncurrentVersionExpiration ToExpiration() => new()
        {
            NoncurrentDays = this.NoncurrentDays,
            NewerNoncurrentVersions = this.NewerNoncurrentVersions,
        };
    }
}
