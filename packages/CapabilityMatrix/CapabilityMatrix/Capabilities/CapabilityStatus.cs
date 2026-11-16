namespace Supabase.CapabilityMatrix.Capabilities;

/// <summary>
/// The status an in-code declaration can express, ordered from the strongest claim to the
/// weakest. A feature that does not apply is declared on the public member that is this
/// SDK's answer to it.
/// </summary>
public enum CapabilityStatus
{
    Implemented,
    Partial,
    NotApplicable,
}

public static class CapabilityStatuses
{
    /// <summary>The spelling the capability matrix expects in <c>sdk-compliance.yaml</c>.</summary>
    public static string InTheMatrix(this CapabilityStatus status) => status switch
    {
        CapabilityStatus.Implemented => "implemented",
        CapabilityStatus.Partial => "partially_implemented",
        CapabilityStatus.NotApplicable => "not_applicable",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, message: null),
    };
}
