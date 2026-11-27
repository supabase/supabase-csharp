using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using Supabase.Core.Attributes;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// Puts the <c>X-Client-Info</c> header on an outgoing request. A value the application already set, under any
/// casing, replaces the SDK's own; the features the request was built with are merged into whichever value is sent.
/// </summary>
internal static class ClientInfoHeader
{
    internal const string Name = "X-Client-Info";

    private const string FeaturesKey = "features";
    private const char SegmentSeparator = ';';
    private const char KeyValueSeparator = '=';
    private const char FeatureSeparator = ',';

    [ImplementedCapability("client.observability.client_info", "Automatic, no API to call: every request of every package carries the structured X-Client-Info header; an X-Client-Info set through the client's headers (SupabaseOptions.Headers, a package's ClientOptions.Headers or per-request headers) replaces the SDK's value.")]
    internal static void AddClientInfo(this HttpHeaders headers, Type clientType, SdkFeatures? features = null)
    {
        var value = WithFeatures(ValueInEffect(headers, ClientInfo.Of(clientType)), features ?? SdkFeatures.None);
        headers.Remove(Name);
        headers.TryAddWithoutValidation(Name, value);
    }

    internal static string Of(Type clientType) => ClientInfo.Of(clientType).HeaderValue;

    private static string ValueInEffect(HttpHeaders headers, ClientInfo sdkValue) =>
        headers.TryGetValues(Name, out var supplied) ? supplied.First() : sdkValue.HeaderValue;

    private static string WithFeatures(string value, SdkFeatures features)
    {
        if (features.Tokens.Count == 0)
            return value;

        var segments = Segments(value);
        var otherSegments = segments.Where(segment => !IsFeaturesParameter(segment));
        return string.Join(ClientInfo.ParameterSeparator, otherSegments.Append(FeaturesParameter(MergedFeatures(segments, features))));
    }

    private static IReadOnlyList<string> Segments(string value) =>
        value.Split(SegmentSeparator).Select(segment => segment.Trim()).Where(segment => segment.Length > 0).ToList();

    private static bool IsFeaturesParameter(string segment) =>
        string.Equals(segment.Split(KeyValueSeparator)[0].Trim(), FeaturesKey, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> MergedFeatures(IEnumerable<string> segments, SdkFeatures features) =>
        segments.Where(IsFeaturesParameter)
            .SelectMany(FeatureTokens)
            .Concat(features.Tokens)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal);

    private static IEnumerable<string> FeatureTokens(string featuresParameter) =>
        featuresParameter.Substring(featuresParameter.IndexOf(KeyValueSeparator) + 1)
            .Split(FeatureSeparator)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0);

    private static string FeaturesParameter(IEnumerable<string> tokens) =>
        $"{FeaturesKey}{KeyValueSeparator}{string.Join(FeatureSeparator.ToString(), tokens)}";
}
