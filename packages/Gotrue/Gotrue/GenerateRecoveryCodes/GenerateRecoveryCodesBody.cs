using System.Text.Json.Serialization;

namespace Supabase.Gotrue.GenerateRecoveryCodes;

internal sealed record GenerateRecoveryCodesBody
{
    private GenerateRecoveryCodesBody(string friendlyName) => this.FriendlyName = friendlyName;

    [JsonPropertyName("friendly_name")]
    public string FriendlyName { get; }

    internal static GenerateRecoveryCodesBody? For(GenerateRecoveryCodesOptions? options) =>
        options?.FriendlyName is null ? null : new GenerateRecoveryCodesBody(options.FriendlyName);
}
