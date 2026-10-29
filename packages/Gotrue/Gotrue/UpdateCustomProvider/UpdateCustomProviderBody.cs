using System;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.UpdateCustomProvider;

internal sealed record UpdateCustomProviderBody : CustomProviderOptionsBody
{
    private UpdateCustomProviderBody(CustomProviderChanges changes)
        : base(changes.Options)
    {
        this.Name = changes.Name;
        this.ClientId = changes.ClientId;
        this.ClientSecret = changes.ClientSecret;
        this.Issuer = changes.Issuer;
        this.AuthorizationUrl = changes.AuthorizationUrl;
        this.TokenUrl = changes.TokenUrl;
        this.UserInfoUrl = changes.UserInfoUrl;
    }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("client_id")]
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientId { get; }

    [JsonPropertyName("client_secret")]
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientSecret { get; }

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

    internal static UpdateCustomProviderBody For(CustomProviderChanges changes) =>
        new(changes ?? throw new ArgumentNullException(nameof(changes)));
}
