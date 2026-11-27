using System.Linq;
using System.Text;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// One <c>key=value</c> parameter of the <c>X-Client-Info</c> value. The value is trimmed and, outside
/// printable ASCII and for <c>;</c> and <c>%</c>, percent-encoded as its UTF-8 bytes.
/// </summary>
internal sealed record ClientInfoParameter(string Key, string Value)
{
    private const byte FirstPrintable = 0x20;
    private const byte LastPrintable = 0x7E;

    internal static ClientInfoParameter? Of(string key, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new ClientInfoParameter(key, value!.Trim());

    public override string ToString() => $"{this.Key}={PercentEncoded(this.Value)}";

    private static string PercentEncoded(string value) => string.Concat(Encoding.UTF8.GetBytes(value).Select(Encoded));

    private static string Encoded(byte octet) => IsSentAsIs(octet) ? ((char) octet).ToString() : $"%{octet:X2}";

    private static bool IsSentAsIs(byte octet) => octet is >= FirstPrintable and <= LastPrintable and not (byte) ';' and not (byte) '%';
}
