using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// Optional settings of an OAuth client. A member left null is not sent: the server applies its default when the
/// client is created, and keeps the current value when it is updated.
/// </summary>
public sealed record OAuthClientOptions
{
    /// <summary>The grants the client may use. The server default is <c>authorization_code</c> and <c>refresh_token</c>.</summary>
    public IReadOnlyList<OAuthClientGrantType>? GrantTypes { get; init; }

    /// <summary>The home page of the client application.</summary>
    public Uri? ClientUri { get; init; }

    /// <summary>The logo of the client application.</summary>
    public Uri? LogoUri { get; init; }
}
