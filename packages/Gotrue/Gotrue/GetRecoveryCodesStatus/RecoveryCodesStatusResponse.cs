using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.GetRecoveryCodesStatus;

internal sealed record RecoveryCodesStatusResponse
{
    private const string NotEnrolledErrorCode = "mfa_factor_not_found";

    internal static RecoveryCodesStatusResponse NotEnrolled { get; } = new() { IsEnrolled = false };

    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("remaining")]
    public int Remaining { get; init; }

    [JsonIgnore]
    public bool IsEnrolled { get; private init; } = true;

    internal static RecoveryCodesStatus? Read(RecoveryCodesStatusResponse? response) =>
        response switch
        {
            null => throw new GotrueException("The auth server returned no recovery codes status."),
            { IsEnrolled: false } => null,
            _ => response.ToRecoveryCodesStatus(),
        };

    internal static bool MeansNotEnrolled(GotrueException rejection) => rejection.ErrorCode == NotEnrolledErrorCode;

    private RecoveryCodesStatus ToRecoveryCodesStatus() =>
        new()
        {
            FactorId = this.Id,
            Total = this.Total,
            Remaining = this.Remaining,
        };
}
