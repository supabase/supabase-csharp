using System;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.CreateCustomProvider;

internal sealed record CreateCustomProviderBody : CustomProviderOptionsBody
{
    private CreateCustomProviderBody(NewCustomProvider provider, CustomProviderOptions? options)
        : base(options)
    {
        this.ProviderType = Core.Helpers.GetMappedToAttr(provider.Type).Mapping;
        this.Identifier = provider.Identifier;
        this.Name = provider.Name;
        this.ClientId = provider.ClientId;
        this.ClientSecret = provider.ClientSecret;
        this.Issuer = provider.Issuer;
        this.AuthorizationUrl = provider.AuthorizationUrl;
        this.TokenUrl = provider.TokenUrl;
        this.UserInfoUrl = provider.UserInfoUrl;
    }

    [JsonPropertyName("provider_type")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ProviderType { get; }

    [JsonPropertyName("identifier")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Identifier { get; }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Name { get; }

    [JsonPropertyName("client_id")]
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ClientId { get; }

    [JsonPropertyName("client_secret")]
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ClientSecret { get; }

    [JsonPropertyName("issuer")]
    [JsonPropertyOrder(14)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Issuer { get; }

    [JsonPropertyName("authorization_url")]
    [JsonPropertyOrder(17)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? AuthorizationUrl { get; }

    [JsonPropertyName("token_url")]
    [JsonPropertyOrder(18)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? TokenUrl { get; }

    [JsonPropertyName("userinfo_url")]
    [JsonPropertyOrder(19)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? UserInfoUrl { get; }

    internal static CreateCustomProviderBody For(NewCustomProvider provider, CustomProviderOptions? options) =>
        new(provider ?? throw new ArgumentNullException(nameof(provider)), options);
}
