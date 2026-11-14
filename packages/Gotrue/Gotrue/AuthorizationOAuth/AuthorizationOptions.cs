namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>Options for handling the browser redirect after an OAuth consent decision.</summary>
public sealed record AuthorizationOptions
{
    /// <summary>When true, return the redirect URL without navigating the browser.</summary>
    public bool SkipBrowserRedirect { get; init; }
}

/// <summary>Options for revoking an OAuth grant.</summary>
public sealed record RevokeGrantOptions
{
    /// <summary>The OAuth client identifier to revoke.</summary>
    public string ClientId { get; init; } = string.Empty;
}
