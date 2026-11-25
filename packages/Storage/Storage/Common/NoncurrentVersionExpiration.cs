namespace Supabase.Storage.Common;

/// <summary>
/// When the previous versions of an object expire.
/// </summary>
public sealed record NoncurrentVersionExpiration
{
    /// <summary>How many days a version must be noncurrent before it expires.</summary>
    public required int NoncurrentDays { get; init; }

    /// <summary>How many of the newest noncurrent versions are kept whatever their age.</summary>
    public int? NewerNoncurrentVersions { get; init; }
}
