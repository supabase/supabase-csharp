namespace Supabase.Gotrue.UpdateFactor;

/// <summary>
/// Changes to a user's MFA factor. A member left null or empty keeps its current value: the auth server cannot clear one.
/// </summary>
public sealed record FactorChanges
{
    /// <summary>The new friendly name.</summary>
    public string? FriendlyName { get; init; }

    /// <summary>
    /// Phone factors only: the new phone number, in E.164 format (for example <c>+15555550100</c>). The server ignores it on
    /// any other factor type.
    /// </summary>
    public string? Phone { get; init; }
}
