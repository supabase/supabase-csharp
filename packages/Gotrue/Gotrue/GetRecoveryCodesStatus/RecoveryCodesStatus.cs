namespace Supabase.Gotrue.GetRecoveryCodesStatus;

/// <summary>
/// How many of the signed-in user's MFA recovery codes remain unused. The codes themselves are never returned.
/// </summary>
public sealed record RecoveryCodesStatus
{
    /// <summary>The id of the user's recovery codes factor, as listed in <see cref="User.Factors" />.</summary>
    public string FactorId { get; init; } = null!;

    /// <summary>The number of codes in the current set.</summary>
    public int Total { get; init; }

    /// <summary>The number of codes in the current set not used yet. Zero once every code has been used.</summary>
    public int Remaining { get; init; }
}
