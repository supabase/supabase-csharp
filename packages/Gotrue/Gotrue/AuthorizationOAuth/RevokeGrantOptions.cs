namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>Options for revoking an OAuth grant.</summary>
public sealed record RevokeGrantOptions
{
    /// <summary>The OAuth client identifier to revoke.</summary>
    public string ClientId { get; init; } = string.Empty;
}
