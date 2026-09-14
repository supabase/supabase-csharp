using System;
using System.Threading.Tasks;
using Supabase.Gotrue.Mfa;
using static Supabase.Gotrue.Constants;
using static Supabase.Gotrue.StatelessClient;

namespace Supabase.Gotrue.Interfaces;

/// <summary>
/// A Stateless Gotrue Client
/// </summary>
/// <example>
/// var options = new StatelessClientOptions { Url = "https://mygotrueurl.com" };
/// var user = await client.SignIn("user@email.com", "fancyPassword", options);
/// </example>
public interface IGotrueStatelessClient<TUser, TSession>
    where TUser : User
    where TSession : Session
{
    /// <summary>
    /// Create a user
    /// </summary>
    /// <param name="serviceRoleToken">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="options"></param>
    /// <param name="attributes"></param>
    /// <returns></returns>
    Task<TUser?> CreateUser(string serviceRoleToken, StatelessClientOptions options, AdminUserAttributes attributes);

    /// <summary>
    /// Create a user
    /// </summary>
    /// <param name="serviceRoleToken">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="options"></param>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="attributes"></param>
    /// <returns></returns>
    Task<TUser?> CreateUser(string serviceRoleToken, StatelessClientOptions options, string email, string password, AdminUserAttributes? attributes = null);

    /// <summary>
    /// Deletes a User.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="serviceRoleToken">this token needs role 'supabase_admin' or 'service_role'</param>
    /// <param name="options"></param>
    /// <param name="shouldSoftDelete">When true, the user is soft-deleted (identifiable by the hashed user id but not reversible) instead of hard-deleted. Defaults to a hard delete.</param>
    /// <returns></returns>
    Task<bool> DeleteUser(string uid, string serviceRoleToken, StatelessClientOptions options, bool shouldSoftDelete = false);

    /// <summary>
    /// Logs in an existing user via a third-party provider.
    /// </summary>
    /// <param name="codeVerifier"></param>
    /// <param name="authCode"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<TSession?> ExchangeCodeForSession(string codeVerifier, string authCode, StatelessClientOptions options);

    /// <summary>
    /// Initialize/retrieve the underlying API for this client
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    IGotrueApi<TUser, TSession> GetApi(StatelessClientOptions options);

    /// <summary>
    /// Parses a <see cref="Session"/> out of a <see cref="Uri"/>'s Query parameters.
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<TSession?> GetSessionFromUrl(Uri uri, StatelessClientOptions options);

    /// <summary>
    /// Get User details by JWT. Can be used to validate a JWT.
    /// </summary>
    /// <param name="serviceRoleToken">A valid JWT. Must be a JWT that originates from a user.</param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<TUser?> GetUser(string serviceRoleToken, StatelessClientOptions options);

    /// <summary>
    /// Get User details by Id
    /// </summary>
    /// <param name="serviceRoleToken">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="options"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<TUser?> GetUserById(string serviceRoleToken, StatelessClientOptions options, string userId);

    /// <summary>
    /// Sends an invite email link to the specified email.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="serviceRoleToken">this token needs role 'supabase_admin' or 'service_role'</param>
    /// <param name="options"></param>
    /// <param name="inviteOptions"></param>
    /// <returns></returns>
    Task<bool> InviteUserByEmail(string email, string serviceRoleToken, StatelessClientOptions options, InviteUserByEmailOptions? inviteOptions = null);

    /// <summary>
    /// Lists users
    /// </summary>
    /// <param name="serviceRoleToken">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="options"></param>
    /// <param name="filter">A string for example part of the email</param>
    /// <param name="sortBy">Snake case string of the given key, currently only created_at is supported</param>
    /// <param name="sortOrder">asc or desc, if null desc is used</param>
    /// <param name="page">page to show for pagination</param>
    /// <param name="perPage">items per page for pagination</param>
    /// <returns></returns>
    Task<UserList<User>?> ListUsers(string serviceRoleToken, StatelessClientOptions options, string? filter = null, string? sortBy = null, SortOrder sortOrder = SortOrder.Descending, int? page = null, int? perPage = null);

    /// <summary>
    /// Refreshes a Token
    /// </summary>
    /// <param name="accessToken"></param>
    /// <param name="refreshToken"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<TSession?> RefreshToken(string accessToken, string refreshToken, StatelessClientOptions options);

    /// <summary>
    /// Sends a reset request to an email address.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    Task<bool> ResetPasswordForEmail(string email, StatelessClientOptions options);

    /// <summary>
    /// Sends a Magic email login link to the specified email.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="options"></param>
    /// <param name="signInOptions"></param>
    /// <returns></returns>
    Task<bool> SendMagicLink(string email, StatelessClientOptions options, SignInOptions? signInOptions = null);

    /// <summary>
    /// Retrieves a Url to redirect to for signing in with a <see cref="Provider"/>.
    /// 
    /// This method will need to be combined with <see cref="GetSessionFromUrl(Uri,StatelessClientOptions)"/> when the
    /// Application receives the Oauth Callback.
    /// </summary>
    /// <example>
    /// var client = Supabase.Gotrue.Client.Initialize(options);
    /// var url = client.SignIn(Provider.Github);
    /// 
    /// // Do Redirect User
    /// 
    /// // Example code
    /// Application.HasReceivedOauth += async (uri) => {
    ///     var session = await client.GetSessionFromUri(uri, true);
    /// }
    /// </example>
    /// <param name="provider"></param>
    /// <param name="options"></param>
    /// <param name="signInOptions"></param>
    /// <returns></returns>
    ProviderAuthState SignIn(Provider provider, StatelessClientOptions options, SignInOptions? signInOptions = null);

    /// <summary>
    /// Log in an existing user, or login via a third-party provider.
    /// </summary>
    /// <param name="type">Type of Credentials being passed</param>
    /// <param name="identifierOrToken">An email, phone, or RefreshToken</param>
    /// <param name="password">Password to account (optional if `RefreshToken`)</param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<TSession?> SignIn(SignInType type, string identifierOrToken, string? password = null, StatelessClientOptions? options = null);

    /// <summary>
    /// Sends a Magic email login link to the specified email.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="options"></param>
    /// <param name="signInOptions"></param>
    /// <returns></returns>
    Task<bool> SignIn(string email, StatelessClientOptions options, SignInOptions? signInOptions = null);

    /// <summary>
    /// Signs in a User with an email address and password.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<TSession?> SignIn(string email, string password, StatelessClientOptions options);

    /// <summary>
    /// Logout a User
    /// This will revoke all refresh tokens for the user.
    /// JWT tokens will still be valid for stateless auth until they expire.
    /// </summary>
    /// <param name="accessToken"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<bool> SignOut(string accessToken, StatelessClientOptions options);

    /// <summary>
    /// Signs up a user
    /// </summary>
    /// <param name="type">Type of signup</param>
    /// <param name="identifier">Phone or Email</param>
    /// <param name="password"></param>
    /// <param name="options"></param>
    /// <param name="signUpOptions">Object containing redirectTo and optional user metadata (data)</param>
    /// <returns></returns>
    Task<TSession?> SignUp(SignUpType type, string identifier, string password, StatelessClientOptions options, SignUpOptions? signUpOptions = null);

    /// <summary>
    /// Signs up a user by email address
    /// </summary>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="options"></param>
    /// <param name="signUpOptions">Object containing redirectTo and optional user metadata (data)</param>
    /// <returns></returns>
    Task<TSession?> SignUp(string email, string password, StatelessClientOptions options, SignUpOptions? signUpOptions = null);

    /// <summary>
    /// Updates a User's attributes
    /// </summary>
    /// <param name="accessToken"></param>
    /// <param name="attributes"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<TUser?> Update(string accessToken, UserAttributes attributes, StatelessClientOptions options);

    /// <summary>
    /// Update user by Id
    /// </summary>
    /// <param name="serviceRoleToken">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="options"></param>
    /// <param name="userId"></param>
    /// <param name="userData"></param>
    /// <returns></returns>
    Task<TUser?> UpdateUserById(string serviceRoleToken, StatelessClientOptions options, string userId, AdminUserAttributes userData);

    /// <summary>
    /// Log in a user given a User supplied OTP received via mobile.
    /// </summary>
    /// <param name="phone">The user's phone number.</param>
    /// <param name="otpToken">Token sent to the user's phone.</param>
    /// <param name="options"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    Task<TSession?> VerifyOTP(string phone, string otpToken, StatelessClientOptions options, MobileOtpType type = MobileOtpType.SMS);

    /// <summary>
    /// Log in a user give a user supplied OTP received via email.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="otpToken"></param>
    /// <param name="options"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    Task<TSession?> VerifyOTP(string email, string otpToken, StatelessClientOptions options, EmailOtpType type = EmailOtpType.MagicLink);

    /// <summary>
    /// Log in a user given the token hash used in an email confirmation link.
    /// </summary>
    /// <param name="tokenHash"></param>
    /// <param name="options"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    Task<TSession?> VerifyTokenHash(string tokenHash, StatelessClientOptions options, EmailOtpType type = EmailOtpType.Email);

    /// <summary>
    /// Retrieve the current settings for the Gotrue instance.
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<Settings?> Settings(StatelessClientOptions options);


    /// <summary>
    /// Starts the enrollment process for a new Multi-Factor Authentication (MFA)
    /// factor. This method creates a new `unverified` factor.
    /// To verify a factor, present the QR code or secret to the user and ask them to add it to their
    /// authenticator app.
    /// The user has to enter the code from their authenticator app to verify it.
    ///
    /// Upon verifying a factor, all other sessions are logged out and the current session's authenticator level is promoted to `aal2`.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="mfaEnrollParams"></param>
    /// <param name="options"></param>
    Task<MfaEnrollResponse?> Enroll(string jwt, MfaEnrollParams mfaEnrollParams, StatelessClientOptions options);

    /// <summary>
    /// Prepares a challenge used to verify that a user has access to a MFA
    /// factor.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="mfaChallengeParams"></param>
    /// <param name="options"></param>
    Task<MfaChallengeResponse?> Challenge(string jwt, MfaChallengeParams mfaChallengeParams, StatelessClientOptions options);

    /// <summary>
    /// Verifies a code against a challenge. The verification code is
    /// provided by the user by entering a code seen in their authenticator app. </summary>
    /// <param name="jwt"></param>
    /// <param name="mfaVerifyParams"></param>
    /// <param name="options"></param>
    Task<MfaVerifyResponse?> Verify(string jwt, MfaVerifyParams mfaVerifyParams, StatelessClientOptions options);

    /// <summary>
    /// Helper method which creates a challenge and immediately uses the given code to verify against it thereafter. The verification code is
    /// provided by the user by entering a code seen in their authenticator app.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="mfaChallengeAndVerifyParams"></param>
    /// <param name="options"></param>
    Task<MfaVerifyResponse?> ChallengeAndVerify(string jwt, MfaChallengeAndVerifyParams mfaChallengeAndVerifyParams, StatelessClientOptions options);

    /// <summary>
    /// Unenroll removes a MFA factor.
    /// A user has to have an `aal2` authenticator level in order to unenroll a `verified` factor.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="mfaUnenrollParams"></param>
    /// <param name="options"></param>
    Task<MfaUnenrollResponse?> Unenroll(string jwt, MfaUnenrollParams mfaUnenrollParams, StatelessClientOptions options);

    /// <summary>
    /// Returns the list of MFA factors enabled for this user
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="options"></param>
    Task<MfaListFactorsResponse?> ListFactors(string jwt, StatelessClientOptions options);

    /// <summary>
    /// Returns the Authenticator Assurance Level (AAL) for the active session.
    ///
    /// - `aal1` (or `null`) means that the user's identity has been verified only
    /// with a conventional login (email+password, OTP, magic link, social login,
    /// etc.).
    /// - `aal2` means that the user's identity has been verified both with a conventional login and at least one MFA factor.
    ///
    /// Although this method returns a promise, it's fairly quick (microseconds)
    /// and rarely uses the network. You can use this to check whether the current
    /// user needs to be shown a screen to verify their MFA factors.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="options"></param>
    Task<MfaGetAuthenticatorAssuranceLevelResponse?> GetAuthenticatorAssuranceLevel(string jwt, StatelessClientOptions options);
}
