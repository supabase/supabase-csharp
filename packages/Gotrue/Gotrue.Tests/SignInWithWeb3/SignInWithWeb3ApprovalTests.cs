#region

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.SignInWithWeb3;

#endregion

namespace Gotrue.Tests.SignInWithWeb3;

/// <summary>
///     Pins the exact bytes a Web3 sign-in puts on the wire: the chain, the message verbatim, the signature in the
///     encoding the auth server reads (base64url for Solana, <c>0x</c>-prefixed hex for Ethereum), and the CAPTCHA token
///     under <c>gotrue_meta_security</c> only when there is one.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignInWithWeb3ApprovalTests : RequestApprovalFixture
{
    private const string SolanaMessage = "localhost:3000 wants you to sign in with your Solana account:\nDRpbCBMxVnDK7maPM5tGv6MvB3v1sRMC86PZ8okm21hy\n\nURI: http://localhost:3000\nVersion: 1\nIssued At: 2026-11-11T10:00:00Z";
    private const string EthereumMessage = "localhost:3000 wants you to sign in with your Ethereum account:\n0x2c7536E3605D9C16a7a3D7b1898e529396a65c23\n\nURI: http://localhost:3000\nVersion: 1\nChain ID: 1\nIssued At: 2026-11-11T10:00:00Z";
    private const string EthereumSignatureHex = "4f8e2b6d1a9c3e7f5b0d8a2c6e4f1b9d7a3c5e8f2b4d6a1c9e7f3b5d8a2c4e6f1b9d7a3c5e8f2b4d6a1c9e7f3b5d8a2c4e6f1b9d7a3c5e8f2b4d6a1c9e7f3b5d1c";
    private static readonly byte[] SolanaSignature = Enumerable.Range(0, 64).Select(index => (byte) (index * 4 + 3)).ToArray();

    [TestInitialize]
    public void AnswerWithSession() => this.RespondTo("/token", Fixture("signed_in_session.json"));

    [TestMethod]
    public async Task SignInWithWeb3Request_ShouldSendTheSolanaSignatureAsBase64Url()
    {
        await this.Api.SignInWithWeb3Async(Web3Credentials.ForSolana(SolanaMessage, SolanaSignature));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task SignInWithWeb3Request_ShouldSendThePrefixedEthereumSignature_GivenSignatureWithPrefix()
    {
        await this.Api.SignInWithWeb3Async(Web3Credentials.ForEthereum(EthereumMessage, "0x" + EthereumSignatureHex));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task SignInWithWeb3Request_ShouldPrefixTheEthereumSignature_GivenSignatureWithoutPrefix()
    {
        await this.Api.SignInWithWeb3Async(Web3Credentials.ForEthereum(EthereumMessage, EthereumSignatureHex));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task SignInWithWeb3Request_ShouldSendTheCaptchaToken_GivenCaptchaToken()
    {
        await this.Api.SignInWithWeb3Async(Web3Credentials.ForSolana(SolanaMessage, SolanaSignature), new SignInWithWeb3Options { CaptchaToken = "a-captcha-token" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task SignInWithWeb3Request_ShouldSendNoSecurityField_GivenEmptyCaptchaToken()
    {
        await this.Api.SignInWithWeb3Async(Web3Credentials.ForSolana(SolanaMessage, SolanaSignature), new SignInWithWeb3Options { CaptchaToken = string.Empty });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignInWithWeb3", "Data", name));
}
