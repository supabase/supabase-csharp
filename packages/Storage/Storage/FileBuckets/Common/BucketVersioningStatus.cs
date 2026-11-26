using System.Text.Json.Serialization;

namespace Supabase.Storage.FileBuckets.Common;

/// <summary>
/// Whether a bucket keeps earlier versions of its objects. A status the service adds later still decodes.
/// </summary>
[JsonConverter(typeof(BucketVersioningStatusConverter))]
public sealed record BucketVersioningStatus
{
    private readonly string name;

    private BucketVersioningStatus(string name) => this.name = name;

    /// <summary>Versioning was never turned on, so the bucket keeps only the current version of each object.</summary>
    public static BucketVersioningStatus Disabled { get; } = new("DISABLED");

    /// <summary>The bucket keeps earlier versions of an object it overwrites or deletes.</summary>
    public static BucketVersioningStatus Enabled { get; } = new("ENABLED");

    /// <summary>The bucket stops keeping new versions but holds on to the ones it has.</summary>
    public static BucketVersioningStatus Suspended { get; } = new("SUSPENDED");

    /// <summary>The name the service uses for the status.</summary>
    public override string ToString() => this.name;

    internal static BucketVersioningStatus FromWire(string name) => new(name);
}
