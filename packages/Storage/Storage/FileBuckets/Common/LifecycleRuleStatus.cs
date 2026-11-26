namespace Supabase.Storage.FileBuckets.Common;

/// <summary>
/// Whether a lifecycle rule runs. A status the service adds later still decodes.
/// </summary>
public sealed record LifecycleRuleStatus
{
    private readonly string name;

    private LifecycleRuleStatus(string name) => this.name = name;

    /// <summary>The rule is applied.</summary>
    public static LifecycleRuleStatus Enabled { get; } = new("Enabled");

    /// <summary>The rule stays stored but is not applied.</summary>
    public static LifecycleRuleStatus Disabled { get; } = new("Disabled");

    /// <summary>The name the service uses for the status.</summary>
    public override string ToString() => this.name;

    internal static LifecycleRuleStatus FromWire(string name) => new(name);
}
