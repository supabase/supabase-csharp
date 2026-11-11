#region

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

#endregion

namespace Gotrue.Tests.SignInWithWeb3;

/// <summary>
///     A software Solana wallet: an ed25519 key pair that writes and signs Sign in with Solana messages for the local
///     app origin, the way a browser wallet's <c>signMessage</c> would.
/// </summary>
internal sealed class SolanaWallet
{
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private readonly Ed25519PrivateKeyParameters privateKey;

    private SolanaWallet(Ed25519PrivateKeyParameters privateKey) => this.privateKey = privateKey;

    internal string Address => EncodeBase58(this.privateKey.GeneratePublicKey().GetEncoded());

    internal static SolanaWallet Create() => new(new Ed25519PrivateKeyParameters(new SecureRandom()));

    internal string SignInMessage() => SignInMessages.ForSolana(this.Address);

    internal byte[] Sign(string message)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, this.privateKey);
        var bytes = Encoding.UTF8.GetBytes(message);
        signer.BlockUpdate(bytes, 0, bytes.Length);
        return signer.GenerateSignature();
    }

    private static string EncodeBase58(byte[] bytes)
    {
        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var digits = new StringBuilder();
        while (value > 0)
        {
            digits.Insert(0, Base58Alphabet[(int) (value % Base58Alphabet.Length)]);
            value /= Base58Alphabet.Length;
        }
        return new string(Base58Alphabet[0], bytes.TakeWhile(b => b == 0).Count()) + digits;
    }
}
