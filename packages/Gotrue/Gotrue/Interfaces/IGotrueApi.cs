using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Interfaces;
using Supabase.Gotrue.Claims;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateCustomProvider;
using Supabase.Gotrue.GenerateRecoveryCodes;
using Supabase.Gotrue.GetRecoveryCodesStatus;
using Supabase.Gotrue.Mfa;
using Supabase.Gotrue.RegisterPasskey;
using Supabase.Gotrue.Resend;
using Supabase.Gotrue.Responses;
using Supabase.Gotrue.SignInWithPasskey;
using Supabase.Gotrue.UpdateCustomProvider;
using Supabase.Gotrue.UpdateFactor;
using static Supabase.Gotrue.Constants;

#pragma warning disable CS1591

namespace Supabase.Gotrue.Interfaces;

public interface IGotrueApi<TUser, TSession> : IGettableHeaders
    where TUser : User
    where TSession : Session
{
    Task<TUser?> CreateUser(string jwt, AdminUserAttributes? attributes = null);
    Task<BaseResponse> DeleteUser(string uid, string jwt, bool shouldSoftDelete = false);
    Task<Jwks?> GetJwksAsync(CancellationToken cancellationToken = default);
    Task<TUser?> GetUser(string jwt);
    Task<TUser?> GetUserAsync(string jwt, CancellationToken cancellationToken = default);
    Task<TUser?> GetUserById(string jwt, string userId);
    Task<BaseResponse> InviteUserByEmail(string email, string jwt, InviteUserByEmailOptions? options = null);
    Task<UserList<TUser>?> ListUsers(string jwt, string? filter = null, string? sortBy = null, SortOrder sortOrder = SortOrder.Descending, int? page = null, int? perPage = null);
    Task<TSession?> RefreshAccessToken(string accessToken, string refreshToken);
    Task<BaseResponse> ResetPasswordForEmail(string email);
    Task<ResetPasswordForEmailState> ResetPasswordForEmail(ResetPasswordForEmailOptions options);
    Task<BaseResponse> SendMagicLinkEmail(string email, SignInOptions? options = null);
    Task<BaseResponse> SendMobileOTP(string phone);
    Task<TSession?> SignInWithIdToken(Provider provider, string idToken, string? accessToken = null, string? nonce = null, string? captchaToken = null);
    Task<TSession?> SignInWithEmail(string email, string password);
    Task<TSession?> SignInWithPhone(string phone, string password);
    Task<TSession?> SignInWithEmail(string email, string password, SignInWithPasswordOptions options);
    Task<TSession?> SignInWithPhone(string phone, string password, SignInWithPasswordOptions options);
    Task<PasswordlessSignInState> SignInWithOtp(SignInWithPasswordlessEmailOptions options);
    Task<PasswordlessSignInState> SignInWithOtp(SignInWithPasswordlessPhoneOptions options);
    Task<TSession?> SignInAnonymously(SignInAnonymouslyOptions? options = null);
    Task<SSOResponse?> SignInWithSSO(Guid providerId, SignInWithSSOOptions? options = null);
    Task<SSOResponse?> SignInWithSSO(string domain, SignInWithSSOOptions? options = null);
    Task<BaseResponse> SignOut(string jwt, SignOutScope scope = SignOutScope.Global);

    /// <summary>
    /// Revokes the sessions of the user who owns <paramref name="jwt" />, within <paramref name="scope" />.
    /// </summary>
    /// <param name="jwt">The user's access token.</param>
    /// <param name="scope">Which of the user's sessions to revoke.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task SignOutAsync(string jwt, SignOutScope scope = SignOutScope.Global, CancellationToken cancellationToken = default);
    Task<TSession?> SignUpWithEmail(string email, string password, SignUpOptions? options = null);
    Task<TSession?> SignUpWithPhone(string phone, string password, SignUpOptions? options = null);
    Task<TUser?> UpdateUser(string jwt, UserAttributes attributes);
    Task<TUser?> UpdateUserById(string jwt, string userId, UserAttributes userData);
    Task<TSession?> VerifyMobileOTP(string phone, string token, MobileOtpType type);
    Task<TSession?> VerifyEmailOTP(string email, string token, EmailOtpType type);
    Task<TSession?> VerifyTokenHash(string tokenHash, EmailOtpType type);
    Task<BaseResponse> Reauthenticate(string userJwt);
    ProviderAuthState GetUriForProvider(Provider provider, SignInOptions? options = null);
    Task<Session?> ExchangeCodeForSession(string codeVerifier, string authCode);
    Task<Settings?> Settings();
    Task<BaseResponse> GenerateLink(string jwt, GenerateLinkOptions options);
    Task<MfaEnrollResponse?> Enroll(string jwt, MfaEnrollParams mfaEnrollParams);
    Task<MfaChallengeResponse?> Challenge(string jwt, MfaChallengeParams mfaChallengeParams);
    Task<MfaVerifyResponse?> Verify(string jwt, MfaVerifyParams mfaVerifyParams);
    Task<MfaUnenrollResponse?> Unenroll(string jwt, MfaUnenrollParams mfaVerifyParams);

    /// <summary>
    /// Reads how many of the user's MFA recovery codes remain unused.
    /// </summary>
    /// <param name="jwt">The user's access token.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The recovery codes status, or null when the user has no recovery codes.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<RecoveryCodesStatus?> GetRecoveryCodesStatusAsync(string jwt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the passkeys the user has registered, oldest first.
    /// </summary>
    /// <param name="jwt">The user's access token.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The user's passkeys, empty when the user has none.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<IReadOnlyList<Passkey>> ListPasskeysAsync(string jwt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one of the user's passkeys.
    /// </summary>
    /// <param name="jwt">The user's access token, at the aal2 assurance level when the user has MFA enabled.</param>
    /// <param name="passkeyId">The id of the passkey.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task DeletePasskeyAsync(string jwt, Guid passkeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames one of the user's passkeys.
    /// </summary>
    /// <param name="jwt">The user's access token.</param>
    /// <param name="passkeyId">The id of the passkey.</param>
    /// <param name="friendlyName">The new name, at most 120 characters.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The passkey with its new name.</returns>
    /// <exception cref="ArgumentException"><paramref name="friendlyName" /> is null or empty. Nothing is sent.</exception>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<Passkey> RenamePasskeyAsync(string jwt, Guid passkeyId, string friendlyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the server for a WebAuthn challenge to register a new passkey for the user.
    /// </summary>
    /// <param name="jwt">The user's access token, at the aal2 assurance level when the user has MFA enabled.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The challenge and the WebAuthn creation options to hand to the platform.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<PasskeyRegistrationChallenge> StartPasskeyRegistrationAsync(string jwt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the credential a WebAuthn ceremony created as one of the user's passkeys.
    /// </summary>
    /// <param name="jwt">The user's access token, at the aal2 assurance level when the user has MFA enabled.</param>
    /// <param name="challengeId">The id of the challenge the credential answers.</param>
    /// <param name="credential">The W3C <c>RegistrationResponseJSON</c> credential, passed to the server verbatim.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The registered passkey.</returns>
    /// <exception cref="ArgumentException"><paramref name="credential" /> is not a JSON object. Nothing is sent.</exception>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<Passkey> RegisterPasskeyAsync(string jwt, Guid challengeId, JsonElement credential, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the server for a passkey sign-in challenge that any of the user's passkeys can answer. No session is needed.
    /// </summary>
    /// <param name="options">The CAPTCHA token, when CAPTCHA protection is enabled.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The challenge and the WebAuthn request options to hand to the platform.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<PasskeySignInChallenge> StartPasskeySignInAsync(SignInWithPasskeyOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a passkey assertion for a session. Nothing is stored: the caller keeps the session.
    /// </summary>
    /// <param name="challengeId">The id of the challenge the assertion answers.</param>
    /// <param name="credential">The W3C <c>AuthenticationResponseJSON</c> assertion, passed to the server verbatim.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The session of the passkey's owner.</returns>
    /// <exception cref="ArgumentException"><paramref name="credential" /> is not a JSON object. Nothing is sent.</exception>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<TSession?> SignInWithPasskeyAsync(Guid challengeId, JsonElement credential, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the user's one-time set of MFA recovery codes.
    /// </summary>
    /// <param name="jwt">The user's access token, at the aal2 assurance level.</param>
    /// <param name="options">Optional settings, such as the factor's friendly name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The generated codes, returned only this once.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<RecoveryCodes> GenerateRecoveryCodesAsync(string jwt, GenerateRecoveryCodesOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the user's MFA recovery codes with a new set, invalidating every previous code.
    /// </summary>
    /// <param name="jwt">The user's access token, at the aal2 assurance level.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new codes, returned only this once.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<RecoveryCodes> RegenerateRecoveryCodesAsync(string jwt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems one of the user's single-use MFA recovery codes for a session at the aal2 assurance level.
    /// </summary>
    /// <param name="jwt">The user's access token.</param>
    /// <param name="code">The recovery code as the user typed it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new aal2 session.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<TSession?> VerifyRecoveryCodeAsync(string jwt, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the user's MFA recovery codes factor together with every remaining code.
    /// </summary>
    /// <param name="jwt">The user's access token, at the aal2 assurance level.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task UnenrollRecoveryCodesAsync(string jwt, CancellationToken cancellationToken = default);
    Task<BaseResponse> ListFactors(string jwt, MfaAdminListFactorsParams listFactorsParams);
    Task<MfaAdminDeleteFactorResponse?> DeleteFactor(string jwt, MfaAdminDeleteFactorParams deleteFactorParams);
    Task<Factor> UpdateFactorAsync(string jwt, string userId, string factorId, FactorChanges changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the passkeys a user has registered, oldest first.
    /// </summary>
    /// <param name="jwt">A full-access API key, such as the service_role key.</param>
    /// <param name="userId">The id of the user.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The user's passkeys, empty when the user has none.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<IReadOnlyList<Passkey>> AdminListPasskeysAsync(string jwt, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one of a user's passkeys.
    /// </summary>
    /// <param name="jwt">A full-access API key, such as the service_role key.</param>
    /// <param name="userId">The id of the user who owns the passkey.</param>
    /// <param name="passkeyId">The id of the passkey.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task AdminDeletePasskeyAsync(string jwt, Guid userId, Guid passkeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a custom OAuth/OIDC provider.
    /// </summary>
    /// <param name="jwt">A full-access API key, such as the service_role key.</param>
    /// <param name="identifier">The provider identifier, starting with <c>custom:</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The identifier lacks the <c>custom:</c> prefix or contains <c>/</c>, <c>\</c>, <c>?</c>, <c>#</c> or <c>%</c>.</exception>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task DeleteCustomProviderAsync(string jwt, string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a custom OAuth/OIDC provider.
    /// </summary>
    /// <param name="jwt">A full-access API key, such as the service_role key.</param>
    /// <param name="provider">The provider to register.</param>
    /// <param name="options">Optional settings; the server applies its defaults to those left null.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The registered provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider" /> is null.</exception>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<CustomProvider> CreateCustomProviderAsync(string jwt, NewCustomProvider provider, CustomProviderOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a custom OAuth/OIDC provider.
    /// </summary>
    /// <param name="jwt">A full-access API key, such as the service_role key.</param>
    /// <param name="identifier">The provider identifier, starting with <c>custom:</c>.</param>
    /// <param name="changes">The changes; members left null keep their current value.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="changes" /> is null.</exception>
    /// <exception cref="ArgumentException">The identifier lacks the <c>custom:</c> prefix or contains <c>/</c>, <c>\</c>, <c>?</c>, <c>#</c> or <c>%</c>.</exception>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<CustomProvider> UpdateCustomProviderAsync(string jwt, string identifier, CustomProviderChanges changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a custom OAuth/OIDC provider by its identifier.
    /// </summary>
    /// <param name="jwt">A full-access API key, such as the service_role key.</param>
    /// <param name="identifier">The provider identifier, starting with <c>custom:</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The provider.</returns>
    /// <exception cref="ArgumentException">The identifier lacks the <c>custom:</c> prefix or contains <c>/</c>, <c>\</c>, <c>?</c>, <c>#</c> or <c>%</c>.</exception>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<CustomProvider> GetCustomProviderAsync(string jwt, string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the custom OAuth/OIDC providers, optionally only those of one type.
    /// </summary>
    /// <param name="jwt">A full-access API key, such as the service_role key.</param>
    /// <param name="type">Only providers of this type; every provider when null.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The providers.</returns>
    /// <exception cref="Exceptions.GotrueException">The server rejected the request.</exception>
    Task<IReadOnlyList<CustomProvider>> ListCustomProvidersAsync(string jwt, CustomProviderType? type = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links an oauth identity to an existing user.
    ///
    /// This method requires the PKCE flow.
    /// </summary>
    /// <param name="token">User's token</param>
    /// <param name="provider">Provider to Link</param>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="Exceptions.GotrueException">Thrown when the server response does not contain a provider authorization url.</exception>
    Task<ProviderAuthState> LinkIdentity(string token, Provider provider, SignInOptions options);

    /// <summary>
    /// Links a native OIDC identity to an existing user using an ID token, authenticated as that user.
    ///
    /// Unlike <see cref="LinkIdentity(string, Provider, SignInOptions)" /> this does not require the PKCE flow.
    /// </summary>
    /// <param name="token">Current user's access token (Bearer JWT) the link is performed on behalf of.</param>
    /// <param name="options">The provider, ID token, and optional proofs describing the identity to link.</param>
    /// <returns>The session GoTrue returns for the linked user.</returns>
    Task<TSession?> LinkIdentityWithIdToken(string token, LinkIdentityWithIdTokenOptions options);

    /// <summary>
    /// Unlinks an identity from a user by deleting it. The user will no longer be able to sign in with that identity once it's unlinked.
    /// </summary>
    /// <param name="token">User's token</param>
    /// <param name="userIdentity">Identity to be unlinked</param>
    /// <returns></returns>
    Task<bool> UnlinkIdentity(string token, UserIdentity userIdentity);

    /// <summary>
    /// Resends a confirmation code to a user's email or phone.
    /// </summary>
    /// <param name="resendParameters"></param>
    /// <returns></returns>
    Task<BaseResponse> Resend(ResendParameters resendParameters);
}
