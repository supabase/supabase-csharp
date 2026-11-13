#region

using System;
using System.Globalization;
using System.Linq;

#endregion

namespace Gotrue.Tests.SignInWithWeb3;

/// <summary>
///     Writes EIP-4361 sign-in messages (Sign in with Ethereum, and its Sign in with Solana port) for the local app
///     origin, <c>http://localhost:3000</c>, which <c>supabase/config.toml</c> allows as a redirect URL.
/// </summary>
internal static class SignInMessages
{
    private const string Domain = "localhost:3000";
    private const string Uri = "http://localhost:3000";
    private const string EthereumMainnetChainId = "Chain ID: 1";

    internal static string ForSolana(string address) => Write("Solana", address);

    internal static string ForEthereum(string address) => Write("Ethereum", address, EthereumMainnetChainId);

    private static string Write(string chain, string address, params string[] chainFields) =>
        string.Join("\n", new[]
            {
                $"{Domain} wants you to sign in with your {chain} account:",
                address,
                string.Empty,
                $"URI: {Uri}",
                "Version: 1",
            }
            .Concat(chainFields)
            .Append($"Issued At: {DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}"));
}
