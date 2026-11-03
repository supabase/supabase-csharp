using System;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.RenamePasskey;

internal sealed record RenamePasskeyBody
{
    private RenamePasskeyBody(string friendlyName) => this.FriendlyName = friendlyName;

    [JsonPropertyName("friendly_name")]
    public string FriendlyName { get; }

    internal static RenamePasskeyBody For(string friendlyName) =>
        string.IsNullOrEmpty(friendlyName)
            ? throw new ArgumentException("A passkey name is required.", nameof(friendlyName))
            : new RenamePasskeyBody(friendlyName);
}
