namespace Supabase.CapabilityMatrix.Capabilities;

/// <summary>
/// How completely a feature is implemented. A member that exists is at least partial,
/// so these are the only two statuses an in-code declaration can express.
/// </summary>
public enum CapabilityStatus
{
    Implemented,
    Partial,
}

public static class CapabilityStatuses
{
    /// <summary>The spelling the capability matrix expects in <c>sdk-compliance.yaml</c>.</summary>
    public static string InTheMatrix(this CapabilityStatus status) => status switch
    {
        CapabilityStatus.Implemented => "implemented",
        CapabilityStatus.Partial => "partially_implemented",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, message: null),
    };
}
