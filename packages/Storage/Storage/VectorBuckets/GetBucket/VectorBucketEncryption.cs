namespace Supabase.Storage.VectorBuckets.GetBucket;

/// <summary>
/// The server-side encryption a vector bucket reports.
/// </summary>
public sealed record VectorBucketEncryption
{
    /// <summary>The server-side encryption type, for example <c>AES256</c> or <c>aws:kms</c>.</summary>
    public string? SseType { get; init; }

    /// <summary>The ARN of the KMS key, reported only for KMS encryption.</summary>
    public string? KmsKeyArn { get; init; }
}
