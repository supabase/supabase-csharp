using System;
using System.Linq;
using Microsoft.IdentityModel.Tokens;

namespace Supabase.Gotrue.SignInWithWeb3;

/// <summary>
/// A sign-in message and the wallet's signature of it, for <see cref="Interfaces.IGotrueClient{TUser,TSession}.SignInWithWeb3Async" />.
/// Create one with <see cref="ForSolana" /> or <see cref="ForEthereum" />.
/// </summary>
public sealed record Web3Credentials
{
    private const string Solana = "solana";
    private const string Ethereum = "ethereum";
    private const int SolanaSignatureLength = 64;
    private const int EthereumSignatureHexLength = 130;
    private const string HexPrefix = "0x";

    private Web3Credentials(string chain, string message, string signature)
    {
        this.Chain = chain;
        this.Message = message;
        this.Signature = signature;
    }

    internal string Chain { get; }

    internal string Message { get; }

    internal string Signature { get; }

    /// <summary>
    /// Credentials for a Solana wallet.
    /// </summary>
    /// <param name="message">
    /// The Sign in with Solana message the wallet signed. It must name the wallet's address and carry
    /// <c>URI</c>, <c>Version</c> and <c>Issued At</c>; its URI must be one of the project's allowed redirect URLs.
    /// </param>
    /// <param name="signature">The wallet's 64-byte ed25519 signature of <paramref name="message" />, as its <c>signMessage</c> returns it.</param>
    /// <returns>The credentials to sign in with.</returns>
    /// <exception cref="ArgumentException"><paramref name="message" /> is blank, or <paramref name="signature" /> is not 64 bytes long.</exception>
    public static Web3Credentials ForSolana(string message, byte[] signature) =>
        new(Solana, RequireMessage(message), EncodeSolanaSignature(signature));

    /// <summary>
    /// Credentials for an Ethereum wallet.
    /// </summary>
    /// <param name="message">
    /// The Sign in with Ethereum (EIP-4361) message the wallet signed. It must name the wallet's address and carry
    /// <c>URI</c>, <c>Version</c>, <c>Chain ID</c> and <c>Issued At</c>; its URI must be one of the project's allowed redirect URLs.
    /// </param>
    /// <param name="signature">
    /// The wallet's 65-byte EIP-191 (<c>personal_sign</c>) signature of <paramref name="message" />, as hex, with or without the <c>0x</c> prefix.
    /// </param>
    /// <returns>The credentials to sign in with.</returns>
    /// <exception cref="ArgumentException"><paramref name="message" /> is blank, or <paramref name="signature" /> is not 65 bytes of hex.</exception>
    public static Web3Credentials ForEthereum(string message, string signature) =>
        new(Ethereum, RequireMessage(message), PrefixEthereumSignature(signature));

    private static string RequireMessage(string message) =>
        string.IsNullOrWhiteSpace(message) ? throw new ArgumentException("The sign-in message must not be blank.", nameof(message)) : message;

    private static string EncodeSolanaSignature(byte[] signature) =>
        signature?.Length == SolanaSignatureLength
            ? Base64UrlEncoder.Encode(signature)
            : throw new ArgumentException("A Solana signature is 64 bytes long.", nameof(signature));

    private static string PrefixEthereumSignature(string signature) =>
        WithoutHexPrefix(signature) is { Length: EthereumSignatureHexLength } hex && hex.All(Uri.IsHexDigit)
            ? HexPrefix + hex
            : throw new ArgumentException("An Ethereum signature is 65 bytes of hex, with or without the 0x prefix.", nameof(signature));

    private static string? WithoutHexPrefix(string? signature) =>
        signature?.StartsWith(HexPrefix, StringComparison.Ordinal) == true ? signature.Substring(HexPrefix.Length) : signature;
}
