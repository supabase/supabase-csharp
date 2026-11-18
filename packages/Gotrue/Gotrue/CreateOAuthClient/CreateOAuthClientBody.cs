using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.CreateOAuthClient;

internal sealed record CreateOAuthClientBody : OAuthClientOptionsBody
{
    private CreateOAuthClientBody(NewOAuthClient client, OAuthClientOptions? options)
        : base(options)
    {
        this.ClientName = client.Name;
        this.ClientType = EnumWireName<OAuthClientType>.Of(client.Type);
        this.TokenEndpointAuthMethod = EnumWireName<OAuthClientTokenEndpointAuthMethod>.Of(client.TokenEndpointAuthMethod);
        this.RedirectUris = client.RedirectUris;
    }

    [JsonPropertyName("client_name")]
    [JsonPropertyOrder(1)]
    public string ClientName { get; }

    [JsonPropertyName("client_type")]
    [JsonPropertyOrder(2)]
    public string ClientType { get; }

    [JsonPropertyName("token_endpoint_auth_method")]
    [JsonPropertyOrder(3)]
    public string TokenEndpointAuthMethod { get; }

    [JsonPropertyName("redirect_uris")]
    [JsonPropertyOrder(4)]
    public IReadOnlyList<Uri> RedirectUris { get; }

    internal static CreateOAuthClientBody For(NewOAuthClient client, OAuthClientOptions? options) =>
        new(client ?? throw new ArgumentNullException(nameof(client)), options);
}
