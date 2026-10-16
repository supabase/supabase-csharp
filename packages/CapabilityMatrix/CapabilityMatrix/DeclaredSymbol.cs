namespace Supabase.CapabilityMatrix;

/// <summary>
/// A single capability annotation found on a declared member.
/// </summary>
/// <param name="Key">The capability-matrix feature id (for example <c>auth.sign_in.sign_up</c>).</param>
/// <param name="Status">Whether the member reports the feature as implemented or partial.</param>
/// <param name="Note">The optional (for implemented) or required (for partial) explanatory note.</param>
public sealed record CapabilityAnnotation(string Key, CapabilityStatus Status, string? Note);

/// <summary>
/// A public-API declaration extracted from the SDK source: a type or a member,
/// named the way <c>sdk-compliance.yaml</c> spells it (the containing-type chain
/// with the namespace stripped, for example <c>Constants.Operator.Equals</c>).
/// </summary>
/// <param name="QualifiedName">The namespace-stripped, containing-type-qualified name.</param>
/// <param name="Kind">The matrix symbol kind: class, method, property, function or variable.</param>
/// <param name="FilePath">Source file the declaration lives in.</param>
/// <param name="Line">1-based line of the declaration.</param>
/// <param name="IsPublic">Whether the declaration (and its whole containing chain) is public.</param>
/// <param name="Annotations">Capability annotations attached to this declaration.</param>
public sealed record DeclaredSymbol(
    string QualifiedName,
    string Kind,
    string FilePath,
    int Line,
    bool IsPublic,
    IReadOnlyList<CapabilityAnnotation> Annotations);
