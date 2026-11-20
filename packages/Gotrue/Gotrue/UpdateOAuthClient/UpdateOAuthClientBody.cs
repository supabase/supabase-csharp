using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.UpdateOAuthClient;

internal sealed record UpdateOAuthClientBody : OAuthClientOptionsBody
{
    private UpdateOAuthClientBody(OAuthClientChanges changes)
        : base(changes.Options)
    {
        this.ClientName = changes.Name;
        this.RedirectUris = changes.RedirectUris;
        this.TokenEndpointAuthMethod = changes.TokenEndpointAuthMethod is { } method ? EnumWireName<OAuthClientTokenEndpointAuthMethod>.Of(method) : null;
    }

    [JsonPropertyName("client_name")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientName { get; }

    [JsonPropertyName("redirect_uris")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Uri>? RedirectUris { get; }

    [JsonPropertyName("token_endpoint_auth_method")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenEndpointAuthMethod { get; }

    internal static UpdateOAuthClientBody For(OAuthClientChanges changes) =>
        new(RequireChange(changes));

    private static OAuthClientChanges RequireChange(OAuthClientChanges changes) =>
        changes is null ? throw new ArgumentNullException(nameof(changes))
        : changes.IsEmpty ? throw new ArgumentException("Set at least one member: the server rejects an update that changes nothing.", nameof(changes))
        : changes;
}
