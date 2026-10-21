namespace Supabase.CapabilityMatrix.Capabilities;

/// <summary>
/// A single capability a member declares about itself: the matrix feature it claims,
/// the status it claims, an optional note, and the public symbol doing the claiming.
/// </summary>
/// <param name="FeatureKey">The matrix feature id, e.g. <c>auth.sign_in.sign_up</c>.</param>
/// <param name="Status">Implemented or partial.</param>
/// <param name="Note">The caveat, required only for partial (enforced by the attribute).</param>
/// <param name="Symbol">The declaring symbol, named as the matrix spells it (e.g. <c>Client.SignUp</c>).</param>
/// <param name="FromPublicApi">Whether that symbol is part of the public API.</param>
public sealed record DeclaredCapability(
    string FeatureKey,
    CapabilityStatus Status,
    string? Note,
    string Symbol,
    bool FromPublicApi);
