namespace Supabase.Gotrue.GenerateRecoveryCodes;

/// <summary>
/// Optional settings for generating the signed-in user's MFA recovery codes.
/// </summary>
public sealed record GenerateRecoveryCodesOptions
{
    /// <summary>
    /// The name of the recovery codes factor shown in the user's factor list. Must be unique among the user's factors.
    /// The server names it <c>Recovery codes</c> when this is null or blank.
    /// </summary>
    public string? FriendlyName { get; init; }
}
