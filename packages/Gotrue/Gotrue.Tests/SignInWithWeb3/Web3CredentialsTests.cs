#region

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.SignInWithWeb3;

#endregion

namespace Gotrue.Tests.SignInWithWeb3;

/// <summary>
///     Pins the checks Web3Credentials makes before anything reaches the wire: a sign-in message must be present, a
///     Solana signature is 64 ed25519 bytes, and an Ethereum signature is 65 bytes of hex, with or without its prefix.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class Web3CredentialsTests
{
    private const string Message = "a-sign-in-message";
    private const int SolanaSignatureLength = 64;
    private const int EthereumSignatureHexLength = 130;

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \n")]
    public void ForSolana_ShouldThrowArgumentException_GivenBlankMessage(string? message)
    {
        var creating = () => Web3Credentials.ForSolana(message!, new byte[SolanaSignatureLength]);
        creating.Should().Throw<ArgumentException>().WithMessage("The sign-in message must not be blank.*").Which.ParamName.Should().Be("message",
            "the server verifies the signature against the message, so there is nothing to sign in with without one");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \n")]
    public void ForEthereum_ShouldThrowArgumentException_GivenBlankMessage(string? message)
    {
        var creating = () => Web3Credentials.ForEthereum(message!, new string('a', EthereumSignatureHexLength));
        creating.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("message",
            "the server verifies the signature against the message, so there is nothing to sign in with without one");
    }

    [TestMethod]
    [DataRow(SolanaSignatureLength - 1)]
    [DataRow(SolanaSignatureLength + 1)]
    [DataRow(0)]
    public void ForSolana_ShouldThrowArgumentException_GivenSignatureNotOf64Bytes(int length)
    {
        var creating = () => Web3Credentials.ForSolana(Message, new byte[length]);
        creating.Should().Throw<ArgumentException>().WithMessage("A Solana signature is 64 bytes long.*").Which.ParamName.Should().Be("signature",
            "an ed25519 signature is 64 bytes (RFC 8032 §5.1.6), and supabase/auth rejects any other length");
    }

    [TestMethod]
    public void ForSolana_ShouldThrowArgumentException_GivenNoSignature()
    {
        var creating = () => Web3Credentials.ForSolana(Message, null!);
        creating.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("signature",
            "a missing signature is caught before the wire, like a malformed one");
    }

    [TestMethod]
    [DataRow(EthereumSignatureHexLength - 1)]
    [DataRow(EthereumSignatureHexLength + 1)]
    [DataRow(0)]
    public void ForEthereum_ShouldThrowArgumentException_GivenSignatureNotOf65Bytes(int length)
    {
        var creating = () => Web3Credentials.ForEthereum(Message, "0x" + new string('a', length));
        creating.Should().Throw<ArgumentException>().WithMessage("An Ethereum signature is 65 bytes of hex, with or without the 0x prefix.*").Which.ParamName.Should().Be("signature",
            "a personal_sign signature is R || S || V, 65 bytes, and supabase/auth rejects any other length");
    }

    [TestMethod]
    public void ForEthereum_ShouldThrowArgumentException_GivenSignatureWithNonHexCharacter()
    {
        var creating = () => Web3Credentials.ForEthereum(Message, new string('a', EthereumSignatureHexLength - 1) + "g");
        creating.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("signature",
            "supabase/auth decodes the signature as hex");
    }

    [TestMethod]
    public void ForEthereum_ShouldThrowArgumentException_GivenNoSignature()
    {
        var creating = () => Web3Credentials.ForEthereum(Message, null!);
        creating.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("signature",
            "a missing signature is caught before the wire, like a malformed one");
    }

    [TestMethod]
    public void ForEthereum_ShouldThrowArgumentException_GivenPrefixAlone()
    {
        var creating = () => Web3Credentials.ForEthereum(Message, "0x");
        creating.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("signature",
            "the prefix carries no signature bytes");
    }

    [TestMethod]
    public void ForEthereum_ShouldThrowArgumentException_GivenUppercasePrefix()
    {
        var creating = () => Web3Credentials.ForEthereum(Message, "0X" + new string('a', EthereumSignatureHexLength));
        creating.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("signature",
            "only the lowercase 0x prefix is stripped, so an uppercase X is a non-hex character");
    }
}
