#region

using System;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

#endregion

namespace Gotrue.Tests.SignInWithWeb3;

/// <summary>
///     A software Ethereum wallet: a secp256k1 key pair that writes Sign in with Ethereum messages for the local app
///     origin and signs them as <c>personal_sign</c> does (EIP-191), returning the 65-byte <c>R || S || V</c> signature
///     as <c>0x</c>-prefixed hex.
/// </summary>
internal sealed class EthereumWallet
{
    private const int ScalarLength = 32;
    private const int AddressLength = 20;
    private const int RecoveryIdOffset = 27;
    private static readonly X9ECParameters Secp256k1 = CustomNamedCurves.GetByName("secp256k1");
    private static readonly ECDomainParameters Domain = new(Secp256k1.Curve, Secp256k1.G, Secp256k1.N, Secp256k1.H);
    private readonly ECPrivateKeyParameters privateKey;
    private readonly byte[] publicKey;

    private EthereumWallet(BigInteger secret)
    {
        this.privateKey = new ECPrivateKeyParameters(secret, Domain);
        this.publicKey = Domain.G.Multiply(secret).Normalize().GetEncoded(false);
    }

    internal string Address => "0x" + ToHex(Keccak256(this.publicKey.Skip(1).ToArray()).TakeLast(AddressLength).ToArray());

    internal static EthereumWallet Create() => new(new BigInteger(Domain.N.BitLength, new SecureRandom()).Mod(Domain.N));

    internal string SignInMessage() => SignInMessages.ForEthereum(this.Address);

    internal string Sign(string message)
    {
        var hash = PersonalMessageHash(message);
        var signer = new ECDsaSigner(new HMacDsaKCalculator(new Sha256Digest()));
        signer.Init(true, this.privateKey);
        var signature = signer.GenerateSignature(hash);
        var r = signature[0];
        var s = LowS(signature[1]);
        var recoveryId = Enumerable.Range(0, 2).First(candidate => this.Recovers(hash, r, s, candidate));
        return "0x" + ToHex(Arrays.ConcatenateAll(
            BigIntegers.AsUnsignedByteArray(ScalarLength, r),
            BigIntegers.AsUnsignedByteArray(ScalarLength, s),
            new[] { (byte) (recoveryId + RecoveryIdOffset) }));
    }

    private bool Recovers(byte[] hash, BigInteger r, BigInteger s, int recoveryId)
    {
        var point = Domain.Curve.DecodePoint(Arrays.Prepend(BigIntegers.AsUnsignedByteArray(ScalarLength, r), (byte) (0x02 + recoveryId)));
        var rInverse = r.ModInverse(Domain.N);
        var e = new BigInteger(1, hash);
        var recovered = ECAlgorithms.SumOfTwoMultiplies(point, s.Multiply(rInverse).Mod(Domain.N), Domain.G, e.Negate().Multiply(rInverse).Mod(Domain.N));
        return recovered.Normalize().GetEncoded(false).SequenceEqual(this.publicKey);
    }

    private static BigInteger LowS(BigInteger s) => s.CompareTo(Domain.N.ShiftRight(1)) > 0 ? Domain.N.Subtract(s) : s;

    private static byte[] PersonalMessageHash(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return Keccak256(Encoding.UTF8.GetBytes($"\u0019Ethereum Signed Message:\n{bytes.Length}").Concat(bytes).ToArray());
    }

    private static byte[] Keccak256(byte[] data)
    {
        var digest = new KeccakDigest(256);
        digest.BlockUpdate(data, 0, data.Length);
        var hash = new byte[digest.GetDigestSize()];
        digest.DoFinal(hash, 0);
        return hash;
    }

    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
