using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Common;

internal abstract record OAuthClientOptionsBody
{
    private static readonly OAuthClientOptions NoOptions = new();

    protected OAuthClientOptionsBody(OAuthClientOptions? options)
    {
        var settings = options ?? NoOptions;
        this.GrantTypes = settings.GrantTypes?.Select(EnumWireName<OAuthClientGrantType>.Of).ToList();
        this.ClientUri = settings.ClientUri;
        this.LogoUri = settings.LogoUri;
    }

    [JsonPropertyName("grant_types")]
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? GrantTypes { get; }

    [JsonPropertyName("client_uri")]
    [JsonPropertyOrder(6)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? ClientUri { get; }

    [JsonPropertyName("logo_uri")]
    [JsonPropertyOrder(7)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? LogoUri { get; }
}
