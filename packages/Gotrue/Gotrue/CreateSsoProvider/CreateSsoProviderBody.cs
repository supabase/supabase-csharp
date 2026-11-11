using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.CreateSsoProvider;

internal sealed record CreateSsoProviderBody
{
    private const string SamlProviderType = "saml";

    private static readonly SsoProviderOptions NoOptions = new();

    private CreateSsoProviderBody(SamlMetadata metadata, SsoProviderOptions options)
    {
        this.MetadataUrl = metadata.Url;
        this.MetadataXml = metadata.Xml;
        this.Domains = options.Domains;
        this.AttributeMapping = SamlAttributeMappingBody.From(options.AttributeMapping);
        this.NameIdFormat = UrnOf(options.NameIdFormat);
        this.ResourceId = options.ResourceId;
        this.Disabled = !options.Enabled;
    }

    [JsonPropertyName("type")]
    public string Type => SamlProviderType;

    [JsonPropertyName("metadata_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? MetadataUrl { get; }

    [JsonPropertyName("metadata_xml")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MetadataXml { get; }

    [JsonPropertyName("domains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Domains { get; }

    [JsonPropertyName("attribute_mapping")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SamlAttributeMappingBody? AttributeMapping { get; }

    [JsonPropertyName("name_id_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NameIdFormat { get; }

    [JsonPropertyName("resource_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceId { get; }

    [JsonPropertyName("disabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Disabled { get; }

    internal static CreateSsoProviderBody For(SamlMetadata metadata, SsoProviderOptions? options) =>
        new(metadata ?? throw new ArgumentNullException(nameof(metadata)), options ?? NoOptions);

    private static string? UrnOf(SamlNameIdFormat? format) =>
        format.HasValue ? Core.Helpers.GetMappedToAttr(format.Value).Mapping : null;
}
