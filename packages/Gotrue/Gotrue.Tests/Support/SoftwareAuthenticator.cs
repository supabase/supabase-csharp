#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     Registers passkeys on the live stack the way a WebAuthn authenticator would, and signs sign-in assertions with
///     them, so the passkey E2E tests can run without a browser or a hardware key. Registration runs the two-step
///     ceremony through the auth server's own API with a plain <see cref="HttpClient" />: it asks
///     <c>POST /passkeys/registration/options</c> for a challenge, answers it with a fresh P-256 key under the
///     <c>none</c> attestation format (the format the server's own tests use), and submits the answer to
///     <c>POST /passkeys/registration/verify</c>. Assertions answer a discoverable sign-in challenge the way
///     supabase/auth's <c>virtualAuthenticator.getAssertion</c> does. The relying party below matches
///     <c>[auth.webauthn]</c> in <c>supabase/config.toml</c>.
/// </summary>
internal static class SoftwareAuthenticator
{
    private const string RelyingPartyId = "localhost";
    private const string Origin = "http://localhost:3000";
    private const byte UserPresentAndAttestedCredentialData = 0x41;
    private const byte UserPresentAndVerified = 0x05;
    private const int CredentialIdLength = 32;
    private const byte CborMapOfThree = 0xA3;
    private const byte CborEmptyMap = 0xA0;
    private const byte CborByteStringType = 0x40;
    private const byte CborTextStringType = 0x60;
    private const byte CborOneByteLength = 24;
    private const byte CborTwoByteLength = 25;
    private const byte CoseXCoordinateLabel = 0x21;
    private const byte CoseYCoordinateLabel = 0x22;

    private static readonly byte[] CoseEc2P256Es256KeyPrefix = { 0xA5, 0x01, 0x02, 0x03, 0x26, 0x20, 0x01 };
    private static readonly byte[] InitialSignCount = new byte[4];
    private static readonly byte[] UnknownAuthenticatorModel = new byte[16];

    private static readonly HttpClient Http = new();

    internal static async Task<Guid> RegisterPasskeyAsync(string accessToken) => (await RegisterSoftwarePasskeyAsync(accessToken)).Id;

    internal static async Task<SoftwarePasskey> RegisterSoftwarePasskeyAsync(string accessToken)
    {
        var challenge = await PostAsync<RegistrationChallenge>("/passkeys/registration/options", accessToken, new { });
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = RandomNumberGenerator.GetBytes(CredentialIdLength);
        var credential = CreateCredential(challenge.Options.Challenge, credentialId, key);
        var passkey = await PostAsync<RegisteredPasskey>("/passkeys/registration/verify", accessToken, new { challenge_id = challenge.ChallengeId, credential });
        return new SoftwarePasskey(passkey.Id, credentialId, key.ExportParameters(true), Base64UrlDecode(challenge.Options.User.Id));
    }

    internal static SoftwarePasskey CreateUnregisteredPasskey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new SoftwarePasskey(Guid.Empty, RandomNumberGenerator.GetBytes(CredentialIdLength), key.ExportParameters(true), Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
    }

    internal static JsonElement SignAssertion(SoftwarePasskey passkey, JsonElement requestOptions)
    {
        using var key = ECDsa.Create(passkey.Key);
        var clientData = JsonSerializer.SerializeToUtf8Bytes(new { type = "webauthn.get", challenge = requestOptions.GetProperty("challenge").GetString(), origin = Origin });
        var authenticatorData = AssertionAuthenticatorData();
        var signature = key.SignData(authenticatorData.Concat(SHA256.HashData(clientData)).ToArray(), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        return JsonSerializer.SerializeToElement(new
        {
            id = Base64Url(passkey.CredentialId),
            rawId = Base64Url(passkey.CredentialId),
            type = "public-key",
            response = new
            {
                clientDataJSON = Base64Url(clientData),
                authenticatorData = Base64Url(authenticatorData),
                signature = Base64Url(signature),
                userHandle = Base64Url(passkey.UserHandle),
            },
        });
    }

    private static async Task<T> PostAsync<T>(string path, string accessToken, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestClients.CliAuthUrl}{path}") { Content = JsonContent.Create(body) };
        request.Headers.Add("apikey", TestClients.CliAnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST {path} answered {(int) response.StatusCode}: {content}");
        return JsonSerializer.Deserialize<T>(content)!;
    }

    private static object CreateCredential(string challenge, byte[] credentialId, ECDsa key)
    {
        var clientData = JsonSerializer.SerializeToUtf8Bytes(new { type = "webauthn.create", challenge, origin = Origin });
        var attestationObject = NoneAttestation(AuthenticatorData(credentialId, key.ExportParameters(false).Q));
        return new
        {
            id = Base64Url(credentialId),
            rawId = Base64Url(credentialId),
            type = "public-key",
            response = new
            {
                clientDataJSON = Base64Url(clientData),
                attestationObject = Base64Url(attestationObject),
                transports = new[] { "internal" },
            },
        };
    }

    private static byte[] AuthenticatorData(byte[] credentialId, ECPoint publicKey) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(RelyingPartyId))
            .Append(UserPresentAndAttestedCredentialData)
            .Concat(InitialSignCount)
            .Concat(UnknownAuthenticatorModel)
            .Concat(BigEndianLength(credentialId.Length))
            .Concat(credentialId)
            .Concat(CoseEs256Key(publicKey))
            .ToArray();

    private static byte[] AssertionAuthenticatorData() =>
        SHA256.HashData(Encoding.UTF8.GetBytes(RelyingPartyId))
            .Append(UserPresentAndVerified)
            .Concat(InitialSignCount)
            .ToArray();

    private static IEnumerable<byte> BigEndianLength(int length) => new[] { (byte) (length >> 8), (byte) length };

    private static byte[] CoseEs256Key(ECPoint publicKey) =>
        CoseEc2P256Es256KeyPrefix
            .Append(CoseXCoordinateLabel).Concat(CborByteString(publicKey.X!))
            .Append(CoseYCoordinateLabel).Concat(CborByteString(publicKey.Y!))
            .ToArray();

    private static byte[] NoneAttestation(byte[] authenticatorData) =>
        new[] { CborMapOfThree }
            .Concat(CborText("fmt")).Concat(CborText("none"))
            .Concat(CborText("attStmt")).Append(CborEmptyMap)
            .Concat(CborText("authData")).Concat(CborByteString(authenticatorData))
            .ToArray();

    private static IEnumerable<byte> CborText(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return CborHeader(CborTextStringType, bytes.Length).Concat(bytes);
    }

    private static IEnumerable<byte> CborByteString(byte[] value) => CborHeader(CborByteStringType, value.Length).Concat(value);

    private static IEnumerable<byte> CborHeader(byte majorType, int length) =>
        length switch
        {
            < CborOneByteLength => new[] { (byte) (majorType | length) },
            <= byte.MaxValue => new[] { (byte) (majorType | CborOneByteLength), (byte) length },
            _ => new[] { (byte) (majorType | CborTwoByteLength) }.Concat(BigEndianLength(length)),
        };

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }

    private sealed record RegistrationChallenge(
        [property: JsonPropertyName("challenge_id")] string ChallengeId,
        [property: JsonPropertyName("options")] CreationOptions Options);

    private sealed record CreationOptions(
        [property: JsonPropertyName("challenge")] string Challenge,
        [property: JsonPropertyName("user")] RelyingPartyUser User);

    private sealed record RelyingPartyUser([property: JsonPropertyName("id")] string Id);

    private sealed record RegisteredPasskey([property: JsonPropertyName("id")] Guid Id);
}
