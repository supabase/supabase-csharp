using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.FileBuckets.Common;

namespace Supabase.Storage.FileBuckets.UpdateBucketLifecycle;

internal sealed class UpdateBucketLifecycleBody
{
    internal UpdateBucketLifecycleBody(BucketLifecycle lifecycle) => this.Rules = lifecycle.Rules.Select(rule => new RuleBody(rule)).ToList();

    [JsonPropertyName("rules")]
    public IReadOnlyList<RuleBody> Rules { get; }

    internal sealed class RuleBody
    {
        internal RuleBody(LifecycleRule rule)
        {
            this.Id = rule.Id;
            this.Status = rule.Status.ToString();
            this.NoncurrentVersionExpiration = rule.NoncurrentVersionExpiration is null ? null : new ExpirationBody(rule.NoncurrentVersionExpiration);
        }

        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; }

        [JsonPropertyName("status")]
        public string Status { get; }

        [JsonPropertyName("filter")]
        public object Filter { get; } = new();

        [JsonPropertyName("noncurrentVersionExpiration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ExpirationBody? NoncurrentVersionExpiration { get; }
    }

    internal sealed class ExpirationBody
    {
        internal ExpirationBody(NoncurrentVersionExpiration expiration)
        {
            this.NoncurrentDays = expiration.NoncurrentDays;
            this.NewerNoncurrentVersions = expiration.NewerNoncurrentVersions;
        }

        [JsonPropertyName("noncurrentDays")]
        public int NoncurrentDays { get; }

        [JsonPropertyName("newerNoncurrentVersions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? NewerNoncurrentVersions { get; }
    }
}
