using System.Collections.Generic;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// The SDK features a request was built with, sent as the <c>features</c> parameter of <c>X-Client-Info</c>.
/// Only the tokens registered in the client-info specification exist here.
/// </summary>
internal sealed record SdkFeatures
{
    private SdkFeatures(params string[] tokens) => this.Tokens = tokens;

    internal IReadOnlyList<string> Tokens { get; }

    internal static SdkFeatures None { get; } = new();

    internal static SdkFeatures TypedApi { get; } = new("typed-api");
}
