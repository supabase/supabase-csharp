using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.UpdateSsoProvider;

internal sealed record UpdateSsoProviderBody
{
    private UpdateSsoProviderBody(SsoProviderChanges changes)
    {
        this.MetadataUrl = changes.Metadata?.Url;
        this.MetadataXml = changes.Metadata?.Xml;
        this.Domains = changes.Domains;
        this.AttributeMapping = SamlAttributeMappingBody.From(changes.AttributeMapping);
        this.NameIdFormat = SamlNameIdFormatUrn.Of(changes.NameIdFormat);
        this.ResourceId = changes.ResourceId;
        this.Disabled = !changes.Enabled;
    }

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

    internal static UpdateSsoProviderBody For(SsoProviderChanges changes) =>
        new(changes ?? throw new ArgumentNullException(nameof(changes)));
}
