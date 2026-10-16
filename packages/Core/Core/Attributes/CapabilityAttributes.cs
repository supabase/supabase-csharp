using System;
using System.Diagnostics.CodeAnalysis;

namespace Supabase.Core.Attributes;

/// <summary>
/// Marks a public member as the implementation of a Supabase capability-matrix
/// feature. The capability-matrix tooling reads these annotations to generate
/// <c>sdk-compliance.yaml</c>, so the mapping lives next to the code it describes
/// and survives renames instead of drifting in a hand-maintained file.
/// </summary>
/// <remarks>
/// The status reported to the matrix is the concrete attribute type:
/// <see cref="ImplementedCapabilityAttribute" /> or
/// <see cref="PartialCapabilityAttribute" />.
/// </remarks>
[ExcludeFromCodeCoverage]
internal abstract class CapabilityAttribute : Attribute
{
    /// <summary>
    /// The capability-matrix feature id this member implements, in
    /// <c>area.group.feature</c> form (for example
    /// <c>auth.sign_in.sign_in_with_password</c>).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// An optional human-readable note surfaced in <c>sdk-compliance.yaml</c>,
    /// typically explaining how this SDK diverges from the canonical behaviour.
    /// </summary>
    public string? Note { get; }

    /// <param name="key">The capability-matrix feature id this member implements.</param>
    /// <param name="note">An optional note surfaced in the generated compliance file.</param>
    protected CapabilityAttribute(string key, string? note)
    {
        this.Key = key;
        this.Note = note;
    }
}

/// <summary>
/// Marks a public member as a fully implemented Supabase capability-matrix feature.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = true)]
[ExcludeFromCodeCoverage]
internal sealed class ImplementedCapabilityAttribute : CapabilityAttribute
{
    /// <param name="key">The capability-matrix feature id this member implements.</param>
    /// <param name="note">
    /// An optional note surfaced in the generated compliance file, for an
    /// implemented feature whose behaviour still warrants a caveat.
    /// </param>
    public ImplementedCapabilityAttribute(string key, string? note = null)
        : base(key, note)
    {
    }
}

/// <summary>
/// Marks a public member as a partially implemented Supabase capability-matrix
/// feature. The note is required: the matrix rejects a <c>partially_implemented</c>
/// entry that does not explain what is missing, so the constructor demands it.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = true)]
[ExcludeFromCodeCoverage]
internal sealed class PartialCapabilityAttribute : CapabilityAttribute
{
    /// <param name="key">The capability-matrix feature id this member partially implements.</param>
    /// <param name="note">A required note explaining what is missing relative to the canonical behaviour.</param>
    public PartialCapabilityAttribute(string key, string note)
        : base(key, note)
    {
    }
}
