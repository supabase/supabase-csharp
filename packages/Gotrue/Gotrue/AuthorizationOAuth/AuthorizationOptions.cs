namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>Options for handling the browser redirect after an OAuth consent decision.</summary>
public sealed record AuthorizationOptions
{
    /// <summary>When true, return the redirect URL without navigating the browser.</summary>
    public bool SkipBrowserRedirect { get; init; }
}
