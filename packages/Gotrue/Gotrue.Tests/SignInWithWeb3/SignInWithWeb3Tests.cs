#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.SignInWithWeb3;

#endregion

namespace Gotrue.Tests.SignInWithWeb3;

/// <summary>
///     End-to-end Web3 sign-ins against the live stack, with both chains enabled under <c>[auth.web3]</c> in
///     <c>supabase/config.toml</c>. Each test creates its own software wallet, so every wallet signs in a user of
///     its own.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SignInWithWeb3Tests : AuthClientFixture
{
    private const string Web3Provider = "web3";

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldSignInTheWalletsUser_GivenSolanaMessageSignedByTheWallet()
    {
        var session = await this.Client.SignInWithWeb3Async(SignedBy(SolanaWallet.Create()));
        session.User!.Identities.Should().ContainSingle().Which.Provider.Should().Be(Web3Provider,
            "a Web3 sign-in creates a user identified by its wallet (supabase/auth web3GrantSolana)");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldSignInTheWalletsUser_GivenEthereumMessageSignedByTheWallet()
    {
        var session = await this.Client.SignInWithWeb3Async(SignedBy(EthereumWallet.Create()));
        session.User!.Identities.Should().ContainSingle().Which.Provider.Should().Be(Web3Provider,
            "a Web3 sign-in creates a user identified by its wallet (supabase/auth web3GrantEthereum)");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldStoreTheSession_GivenSignedMessage()
    {
        var session = await this.Client.SignInWithWeb3Async(SignedBy(SolanaWallet.Create()));
        this.VerifyGoodSession(session);
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldSignInTheSameUser_GivenTheSameWalletSigningInAgain()
    {
        var wallet = SolanaWallet.Create();
        var first = await this.Client.SignInWithWeb3Async(SignedBy(wallet));
        var second = await this.Client.SignInWithWeb3Async(SignedBy(wallet));
        second.User!.Id.Should().Be(first.User!.Id, "the wallet's address is the user's identity, so it signs in the user it created");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldThrow_GivenMessageSignedByAnotherWallet()
    {
        var wallet = EthereumWallet.Create();
        var message = wallet.SignInMessage();
        var signingIn = () => this.Client.SignInWithWeb3Async(Web3Credentials.ForEthereum(message, EthereumWallet.Create().Sign(message)));
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.Message.Should().Contain("Signature does not match address in message",
            "the server recovers the signer and rejects a signature from any wallet but the one named in the message (supabase/auth web3GrantEthereum)");
    }

    private static Web3Credentials SignedBy(SolanaWallet wallet)
    {
        var message = wallet.SignInMessage();
        return Web3Credentials.ForSolana(message, wallet.Sign(message));
    }

    private static Web3Credentials SignedBy(EthereumWallet wallet)
    {
        var message = wallet.SignInMessage();
        return Web3Credentials.ForEthereum(message, wallet.Sign(message));
    }
}
