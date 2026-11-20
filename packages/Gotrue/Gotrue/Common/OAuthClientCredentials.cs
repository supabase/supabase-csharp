using System.Text;

namespace Supabase.Gotrue.Common;

/// <summary>
/// An OAuth client together with its client secret. The server shows the secret once, when the client is created or
/// its secret is regenerated, and keeps only a hash of it: store it before discarding this value.
/// </summary>
public sealed record OAuthClientCredentials
{
    /// <summary>The client.</summary>
    public OAuthClient Client { get; init; } = null!;

    /// <summary>
    /// The client secret, or null for a public client, which has none. It is left out of <c>ToString()</c> so that
    /// it never reaches a log.
    /// </summary>
    public string? ClientSecret { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Client = {this.Client}");
        return true;
    }
}
