using System.Collections.Generic;
using System.Text.Json;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

namespace Supabase.Gotrue.Exceptions;

/// <summary>
/// Maps Supabase server errors to hints from the machine-readable <c>error_code</c> the GoTrue server
/// returns, falling back to the HTTP status code for responses that carry no classifiable body.
/// </summary>
public static class FailureHint
{
    /// <summary>
    /// Best effort guess at why the exception was thrown.
    /// </summary>
    public enum Reason
    {
        /// <summary>
        /// The reason for the error could not be determined.
        /// </summary>
        Unknown,

        /// <summary>
        /// The client is set to run offline or the network is unavailable.
        /// </summary>
        Offline,

        /// <summary>
        /// The user's email address has not been confirmed.
        /// </summary>
        UserEmailNotConfirmed,

        /// <summary>
        /// The user's email address and password are invalid.
        /// </summary>
        UserBadMultiple,

        /// <summary>
        /// The user's password is invalid.
        /// </summary>
        UserBadPassword,

        /// <summary>
        /// The user's login is invalid.
        /// </summary>
        UserBadLogin,

        /// <summary>
        /// The user's email address is invalid.
        /// </summary>
        UserBadEmailAddress,

        /// <summary>
        /// The user's phone number is invalid.
        /// </summary>
        UserBadPhoneNumber,

        /// <summary>
        /// The user's information is incomplete.
        /// </summary>
        UserMissingInformation,

        /// <summary>
        /// The user is already registered.
        /// </summary>
        UserAlreadyRegistered,

        /// <summary>
        /// Server rejected due to number of requests
        /// </summary>
        UserTooManyRequests,

        /// <summary>
        /// The refresh token is invalid.
        /// </summary>
        InvalidRefreshToken,

        /// <summary>
        /// The refresh token expired.
        /// </summary>
        ExpiredRefreshToken,

        /// <summary>
        /// This operation requires a bearer/service key (do not include this key in a client app)
        /// </summary>
        AdminTokenRequired,

        /// <summary>
        /// No/invalid session found
        /// </summary>
        NoSessionFound,

        /// <summary>
        /// Something wrong with the URL to session transformation
        /// </summary>
        BadSessionUrl,

        /// <summary>
        /// An invalid authentication flow has been selected.
        /// </summary>
        InvalidFlowType,

        /// <summary>
        /// The SSO domain provided was not registered via the CLI
        /// </summary>
        SsoDomainNotFound,

        /// <summary>
        /// The sso provider ID was incorrect or does not exist
        /// </summary>
        SsoProviderNotFound,

        /// <summary>
        /// Indicates that the multi-factor authentication (MFA) challenge was not successfully verified.
        /// </summary>
        MfaChallengeUnverified,

        /// <summary>
        /// 502, 503, 504: Standard server/gateway errors
        /// </summary>
        NetworkError,

        /// <summary>
        /// Indicates an error related to Cloudflare network connectivity or operation.
        /// 520-524, 530: Cloudflare-specific error codes (web server down, connection timed out, etc.)
        /// </summary>
        CloudflareNetworkError,
    }

    /// <summary>
    /// The GoTrue server has returned a machine-readable <c>error_code</c> in every error body since
    /// early 2024. This maps the codes we recognise onto a <see cref="Reason"/>; anything absent here
    /// (including generic codes such as <c>validation_failed</c>) resolves to <see cref="Reason.Unknown"/>,
    /// with the raw code still available on <see cref="GotrueException.ErrorCode"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Reason> ReasonByErrorCode = new Dictionary<string, Reason>
    {
        ["invalid_credentials"] = UserBadLogin,
        ["email_not_confirmed"] = UserEmailNotConfirmed,
        ["email_address_invalid"] = UserBadEmailAddress,
        ["refresh_token_not_found"] = InvalidRefreshToken,
        ["refresh_token_already_used"] = InvalidRefreshToken,
        ["user_already_exists"] = UserAlreadyRegistered,
        ["email_exists"] = UserAlreadyRegistered,
        ["phone_exists"] = UserAlreadyRegistered,
        ["weak_password"] = UserBadPassword,
        ["over_request_rate_limit"] = UserTooManyRequests,
        ["over_email_send_rate_limit"] = UserTooManyRequests,
        ["over_sms_send_rate_limit"] = UserTooManyRequests,
        ["bad_jwt"] = AdminTokenRequired,
        ["no_authorization"] = AdminTokenRequired,
        ["not_admin"] = AdminTokenRequired,
        ["sso_provider_not_found"] = SsoProviderNotFound,
        ["mfa_verification_failed"] = MfaChallengeUnverified,
        ["mfa_verification_rejected"] = MfaChallengeUnverified,
        ["mfa_challenge_expired"] = MfaChallengeUnverified,
    };

    /// <summary>
    /// Reads the machine-readable <c>error_code</c> from a GoTrue error body. Returns null for bodies
    /// that are missing, not JSON (gateway/Cloudflare pages), or that carry no <c>error_code</c> field.
    /// </summary>
    /// <param name="content">The raw error response body.</param>
    public static string? ParseErrorCode(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            return document.RootElement.TryGetProperty("error_code", out var errorCode) && errorCode.ValueKind == JsonValueKind.String
                ? errorCode.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Detects the reason for the error from the server's machine-readable <c>error_code</c>, falling back
    /// to the HTTP status code for responses that carry no classifiable body (rate limiting, and gateway
    /// or Cloudflare errors served as HTML rather than JSON).
    /// </summary>
    /// <param name="gte"></param>
    /// <returns></returns>
    public static Reason DetectReason(GotrueException gte)
    {
        var errorCode = gte.ErrorCode ?? ParseErrorCode(gte.Content);
        if (errorCode != null && ReasonByErrorCode.TryGetValue(errorCode, out var reasonFromCode))
            return reasonFromCode;

        return gte.StatusCode switch
        {
            429 => UserTooManyRequests,
            502 or 503 or 504 => NetworkError,
            520 or 521 or 522 or 523 or 524 or 530 => CloudflareNetworkError,
            _ => Unknown,
        };
    }
}
