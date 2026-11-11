using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.Common;

internal sealed record SamlConfigurationResponse
{
    private static readonly IReadOnlyDictionary<string, SamlNameIdFormat> NameIdFormatsByUrn =
        Enum.GetValues(typeof(SamlNameIdFormat)).Cast<SamlNameIdFormat>()
            .ToDictionary(format => Core.Helpers.GetMappedToAttr(format).Mapping);

    [JsonPropertyName("entity_id")]
    public string EntityId { get; init; } = null!;

    [JsonPropertyName("metadata_xml")]
    public string? MetadataXml { get; init; }

    [JsonPropertyName("metadata_url")]
    public Uri? MetadataUrl { get; init; }

    [JsonPropertyName("attribute_mapping")]
    public SamlAttributeMappingResponse? AttributeMapping { get; init; }

    [JsonPropertyName("name_id_format")]
    public string? NameIdFormat { get; init; }

    internal SamlConfiguration ToSamlConfiguration() =>
        new()
        {
            EntityId = this.EntityId,
            MetadataXml = this.MetadataXml,
            MetadataUrl = this.MetadataUrl,
            AttributeMapping = SamlAttributeMappingResponse.Read(this.AttributeMapping),
            NameIdFormat = this.NameIdFormat is { } urn ? ParseNameIdFormat(urn) : null,
        };

    private static SamlNameIdFormat ParseNameIdFormat(string urn) =>
        NameIdFormatsByUrn.TryGetValue(urn, out var format)
            ? format
            : throw new GotrueException($"The auth server returned an unrecognized SAML NameID format '{urn}'.");
}
