using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Common;

internal abstract record CustomProviderOptionsBody
{
    private static readonly CustomProviderOptions NoOptions = new();

    protected CustomProviderOptionsBody(CustomProviderOptions? options)
    {
        var settings = options ?? NoOptions;
        this.AcceptableClientIds = settings.AcceptableClientIds;
        this.Scopes = settings.Scopes;
        this.PkceEnabled = settings.PkceEnabled;
        this.AttributeMapping = settings.AttributeMapping;
        this.CustomClaimsAllowlist = settings.CustomClaimsAllowlist;
        this.AuthorizationParameters = settings.AuthorizationParameters;
        this.Enabled = settings.Enabled;
        this.EmailOptional = settings.EmailOptional;
        this.DiscoveryUrl = settings.DiscoveryUrl;
        this.SkipNonceCheck = settings.SkipNonceCheck;
        this.JwksUri = settings.JwksUri;
    }

    [JsonPropertyName("acceptable_client_ids")]
    [JsonPropertyOrder(6)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AcceptableClientIds { get; }

    [JsonPropertyName("scopes")]
    [JsonPropertyOrder(7)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Scopes { get; }

    [JsonPropertyName("pkce_enabled")]
    [JsonPropertyOrder(8)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PkceEnabled { get; }

    [JsonPropertyName("attribute_mapping")]
    [JsonPropertyOrder(9)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object>? AttributeMapping { get; }

    [JsonPropertyName("custom_claims_allowlist")]
    [JsonPropertyOrder(10)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? CustomClaimsAllowlist { get; }

    [JsonPropertyName("authorization_params")]
    [JsonPropertyOrder(11)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? AuthorizationParameters { get; }

    [JsonPropertyName("enabled")]
    [JsonPropertyOrder(12)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; }

    [JsonPropertyName("email_optional")]
    [JsonPropertyOrder(13)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EmailOptional { get; }

    [JsonPropertyName("discovery_url")]
    [JsonPropertyOrder(15)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? DiscoveryUrl { get; }

    [JsonPropertyName("skip_nonce_check")]
    [JsonPropertyOrder(16)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SkipNonceCheck { get; }

    [JsonPropertyName("jwks_uri")]
    [JsonPropertyOrder(20)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? JwksUri { get; }
}
