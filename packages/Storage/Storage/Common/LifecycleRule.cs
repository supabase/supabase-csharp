namespace Supabase.Storage.Common;

/// <summary>
/// One rule of a bucket's lifecycle policy.
/// </summary>
public sealed record LifecycleRule
{
    /// <summary>The name of the rule, unique within the policy.</summary>
    public string? Id { get; init; }

    /// <summary>Whether the rule runs.</summary>
    public required LifecycleRuleStatus Status { get; init; }

    /// <summary>When previous versions of an object expire. Null when the rule uses another action.</summary>
    public NoncurrentVersionExpiration? NoncurrentVersionExpiration { get; init; }
}
