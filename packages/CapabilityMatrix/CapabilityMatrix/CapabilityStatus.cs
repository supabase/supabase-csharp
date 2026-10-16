namespace Supabase.CapabilityMatrix;

/// <summary>
/// The compliance status a capability annotation reports to the matrix. Mirrors
/// the subset of matrix statuses an in-code annotation can express — a member
/// that exists is, by definition, at least partially implemented.
/// </summary>
public enum CapabilityStatus
{
    Implemented,
    PartiallyImplemented,
}

public static class CapabilityStatusExtensions
{
    /// <summary>The canonical matrix spelling, as written in <c>sdk-compliance.yaml</c>.</summary>
    public static string ToYamlValue(this CapabilityStatus status) => status switch
    {
        CapabilityStatus.Implemented => "implemented",
        CapabilityStatus.PartiallyImplemented => "partially_implemented",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
