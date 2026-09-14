#region

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Supabase.Core.Http;
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue.Mfa;
using static Supabase.Gotrue.Constants;

#endregion

namespace Supabase.Gotrue;

/// <inheritdoc />
public class StatelessClient : IGotrueStatelessClient<User, Session>
{
    /// <inheritdoc />
    public async Task<Settings?> Settings(StatelessClientOptions options)
    {
        var api = this.GetApi(options);
        return await api.Settings();
    }

    /// <inheritdoc />
    public async Task<MfaEnrollResponse?> Enroll(string jwt, MfaEnrollParams mfaEnrollParams, StatelessClientOptions options) => await this.GetApi(options).Enroll(jwt, mfaEnrollParams);

    /// <inheritdoc />
    public async Task<MfaChallengeResponse?> Challenge(string jwt, MfaChallengeParams mfaChallengeParams, StatelessClientOptions options) => await this.GetApi(options).Challenge(jwt, mfaChallengeParams);

    /// <inheritdoc />
    public async Task<MfaVerifyResponse?> Verify(string jwt, MfaVerifyParams mfaVerifyParams, StatelessClientOptions options) => await this.GetApi(options).Verify(jwt, mfaVerifyParams);

    /// <inheritdoc />
    public async Task<MfaVerifyResponse?> ChallengeAndVerify(string jwt, MfaChallengeAndVerifyParams mfaChallengeAndVerifyParams, StatelessClientOptions options)
    {
        var api = this.GetApi(options);
        var challengeResponse = await api.Challenge(jwt, new MfaChallengeParams
        {
            FactorId = mfaChallengeAndVerifyParams.FactorId,
        });
        if (challengeResponse != null)
        {
            var verifyResponse = await api.Verify(jwt, new MfaVerifyParams
            {
                FactorId = mfaChallengeAndVerifyParams.FactorId,
                ChallengeId = challengeResponse.Id,
                Code = mfaChallengeAndVerifyParams.Code,
            });
            return verifyResponse;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<MfaUnenrollResponse?> Unenroll(string jwt, MfaUnenrollParams mfaUnenrollParams, StatelessClientOptions options) => await this.GetApi(options).Unenroll(jwt, mfaUnenrollParams);

    /// <inheritdoc />
    public async Task<MfaListFactorsResponse?> ListFactors(string jwt, StatelessClientOptions options)
    {
        var api = this.GetApi(options);
        var user = await api.GetUser(jwt);
        if (user != null)
        {
            var response = new MfaListFactorsResponse
            {
                All = user.Factors,
                Totp = user.Factors.Where(x => x.FactorType == "totp" && x.Status == "verified").ToList(),
            };
            return response;
        }
        return null;
    }
    public async Task<MfaGetAuthenticatorAssuranceLevelResponse?> GetAuthenticatorAssuranceLevel(string jwt, StatelessClientOptions options)
    {
        var api = this.GetApi(options);
        var user = await api.GetUser(jwt);
        if (user != null)
        {
            var payload = new JwtSecurityTokenHandler().ReadJwtToken(jwt).Payload;
            if (payload == null || payload.ValidTo == DateTime.MinValue)
            {
                throw new Exception("`accessToken`'s payload was of an unknown structure.");
            }
            AuthenticatorAssuranceLevel? currentLevel = null;
            if (payload.ContainsKey("aal"))
            {
                currentLevel = Enum.TryParse(payload["aal"].ToString(), out AuthenticatorAssuranceLevel parsedLevel) ? parsedLevel : (AuthenticatorAssuranceLevel?) null;
            }
            var nextLevel = currentLevel;
            var verifiedFactors = user.Factors?.Where(factor => factor.Status == "verified").ToList() ?? new List<Factor>();
            if (verifiedFactors.Count > 0)
            {
                nextLevel = AuthenticatorAssuranceLevel.aal2;
            }
            var currentAuthenticationMethods = payload.Amr.Select(x => JsonSerializer.Deserialize<AmrEntry>(x, Helpers.SerializerOptions));
            var response = new MfaGetAuthenticatorAssuranceLevelResponse
            {
                CurrentLevel = currentLevel,
                NextLevel = nextLevel,
                CurrentAuthenticationMethods = currentAuthenticationMethods.ToArray(),
            };
            return response;
        }
        return null;
    }

    /// <inheritdoc />
    public IGotrueApi<User, Session> GetApi(StatelessClientOptions options) =>
        new Api(options.Url, options.Headers, options.HttpClient ?? (options.Proxy != null ? DefaultHttpClientFactory.Create(proxy: options.Proxy) : null), options.Retry);

    /// <inheritdoc />
    public Task<Session?> SignUp(string email, string password, StatelessClientOptions options, SignUpOptions? signUpOptions = null) => this.SignUp(SignUpType.Email, email, password, options, signUpOptions);

    /// <inheritdoc />
    public async Task<Session?> SignUp(SignUpType type, string identifier, string password, StatelessClientOptions options, SignUpOptions? signUpOptions = null)
    {
        var api = this.GetApi(options);
        // Return pending users regardless of AllowUnconfirmedUserSessions; this client stores no session.
        return type switch
        {
            SignUpType.Email => await api.SignUpWithEmail(identifier, password, signUpOptions).ConfigureAwait(false),
            SignUpType.Phone => await api.SignUpWithPhone(identifier, password, signUpOptions).ConfigureAwait(false),
            _ => null,
        };
    }

    /// <inheritdoc />
    public async Task<bool> SignIn(string email, StatelessClientOptions options, SignInOptions? signInOptions = null)
    {
        await this.GetApi(options).SendMagicLinkEmail(email, signInOptions);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> SendMagicLink(string email, StatelessClientOptions options, SignInOptions? signInOptions = null) => this.SignIn(email, options, signInOptions);

    /// <inheritdoc />
    public Task<Session?> SignIn(string email, string password, StatelessClientOptions options) => this.SignIn(SignInType.Email, email, password, options);

    /// <inheritdoc />
    public async Task<Session?> SignIn(SignInType type, string identifierOrToken, string? password = null, StatelessClientOptions? options = null)
    {
        options ??= new StatelessClientOptions();
        var api = this.GetApi(options);
        Session? session;
        switch (type)
        {
            case SignInType.Email:
                session = await api.SignInWithEmail(identifierOrToken, password!);
                break;
            case SignInType.Phone:
                if (string.IsNullOrEmpty(password))
                {
                    await api.SendMobileOTP(identifierOrToken);
                    return null;
                }
                session = await api.SignInWithPhone(identifierOrToken, password!);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
        if (session?.User?.IsConfirmed == true || session?.User != null && options.AllowUnconfirmedUserSessions)
        {
            return session;
        }
        return null;
    }

    /// <inheritdoc />
    public ProviderAuthState SignIn(Provider provider, StatelessClientOptions options, SignInOptions? signInOptions = null) => this.GetApi(options).GetUriForProvider(provider, signInOptions);

    /// <inheritdoc />
    public async Task<bool> SignOut(string accessToken, StatelessClientOptions options)
    {
        var result = await this.GetApi(options).SignOut(accessToken);
        result.ResponseMessage?.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<Session?> VerifyOTP(string phone, string otpToken, StatelessClientOptions options, MobileOtpType type = MobileOtpType.SMS)
    {
        var session = await this.GetApi(options).VerifyMobileOTP(phone, otpToken, type);
        if (session?.AccessToken != null)
        {
            return session;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<Session?> VerifyOTP(string email, string otpToken, StatelessClientOptions options, EmailOtpType type = EmailOtpType.MagicLink)
    {
        var session = await this.GetApi(options).VerifyEmailOTP(email, otpToken, type);
        if (session?.AccessToken != null)
        {
            return session;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<Session?> VerifyTokenHash(string tokenHash, StatelessClientOptions options, EmailOtpType type = EmailOtpType.Email)
    {
        var session = await this.GetApi(options).VerifyTokenHash(tokenHash, type);
        if (session?.AccessToken != null)
        {
            return session;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<User?> Update(string accessToken, UserAttributes attributes, StatelessClientOptions options)
    {
        var result = await this.GetApi(options).UpdateUser(accessToken, attributes);
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> InviteUserByEmail(string email, string serviceRoleToken, StatelessClientOptions options, InviteUserByEmailOptions? invitationOptions = null)
    {
        var response = await this.GetApi(options).InviteUserByEmail(email, serviceRoleToken, invitationOptions);
        response.ResponseMessage?.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ResetPasswordForEmail(string email, StatelessClientOptions options)
    {
        var result = await this.GetApi(options).ResetPasswordForEmail(email);
        result.ResponseMessage?.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<UserList<User>?> ListUsers(string serviceRoleToken, StatelessClientOptions options, string? filter = null, string? sortBy = null, SortOrder sortOrder = SortOrder.Descending,
        int? page = null, int? perPage = null) => await this.GetApi(options).ListUsers(serviceRoleToken, filter, sortBy, sortOrder, page, perPage);

    /// <inheritdoc />
    public async Task<User?> GetUserById(string serviceRoleToken, StatelessClientOptions options, string userId) => await this.GetApi(options).GetUserById(serviceRoleToken, userId);

    /// <inheritdoc />
    public async Task<User?> GetUser(string serviceRoleToken, StatelessClientOptions options) => await this.GetApi(options).GetUser(serviceRoleToken);

    /// <inheritdoc />
    public Task<User?> CreateUser(string serviceRoleToken, StatelessClientOptions options, string email, string password, AdminUserAttributes? attributes = null)
    {
        attributes ??= new AdminUserAttributes();
        attributes.Email = email;
        attributes.Password = password;
        return this.CreateUser(serviceRoleToken, options, attributes);
    }

    /// <inheritdoc />
    public async Task<User?> CreateUser(string serviceRoleToken, StatelessClientOptions options, AdminUserAttributes attributes) => await this.GetApi(options).CreateUser(serviceRoleToken, attributes);

    /// <inheritdoc />
    public async Task<User?> UpdateUserById(string serviceRoleToken, StatelessClientOptions options, string userId, AdminUserAttributes userData) =>
        await this.GetApi(options).UpdateUserById(serviceRoleToken, userId, userData);

    /// <inheritdoc />
    public async Task<bool> DeleteUser(string uid, string serviceRoleToken, StatelessClientOptions options, bool shouldSoftDelete = false)
    {
        var result = await this.GetApi(options).DeleteUser(uid, serviceRoleToken, shouldSoftDelete);
        result.ResponseMessage?.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<Session?> ExchangeCodeForSession(string codeVerifier, string authCode, StatelessClientOptions options) => await this.GetApi(options).ExchangeCodeForSession(codeVerifier, authCode);

    /// <inheritdoc />
    public async Task<Session?> GetSessionFromUrl(Uri uri, StatelessClientOptions options)
    {
        var query = HttpUtility.ParseQueryString(uri.Query);
        var errorDescription = query.Get("error_description");
        if (!string.IsNullOrEmpty(errorDescription))
        {
            throw new Exception(errorDescription);
        }
        var accessToken = query.Get("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new Exception("No access_token detected.");
        }
        var expiresIn = query.Get("expires_in");
        if (string.IsNullOrEmpty(expiresIn))
        {
            throw new Exception("No expires_in detected.");
        }
        var refreshToken = query.Get("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new Exception("No refresh_token detected.");
        }
        var tokenType = query.Get("token_type");
        if (string.IsNullOrEmpty(tokenType))
        {
            throw new Exception("No token_type detected.");
        }
        var user = await this.GetApi(options).GetUser(accessToken);
        var session = new Session
        {
            AccessToken = accessToken,
            ExpiresIn = long.Parse(expiresIn),
            RefreshToken = refreshToken,
            TokenType = tokenType,
            User = user,
        };
        return session;
    }

    /// <inheritdoc />
    public async Task<Session?> RefreshToken(string accessToken, string refreshToken, StatelessClientOptions options) =>
        await this.GetApi(options).RefreshAccessToken(accessToken, refreshToken);

    /// <summary>
    ///     Class representation options available to the <see cref="Client" />.
    /// </summary>
    public class StatelessClientOptions
    {

        /// <summary>
        ///     Headers to be sent with subsequent requests.
        /// </summary>
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();

        /// <summary>
        ///     Gotrue Endpoint
        /// </summary>
        public string Url { get; set; } = GOTRUE_URL;

        /// <summary>
        ///     An HttpClient to send requests through. When null, the client builds and owns its own.
        /// </summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>
        ///     A proxy to route requests through. Only used when <see cref="HttpClient"/> is not supplied.
        /// </summary>
        public IWebProxy? Proxy { get; set; }

        /// <summary>
        ///     Retry policy applied to each request. Default (<see cref="RetryOptions.MaxRetries"/> 0) sends once, unretried.
        /// </summary>
        public RetryOptions Retry { get; set; } = new RetryOptions();

        /// <summary>
        ///     Allows sign-in to return a session for an unconfirmed user. Defaults to false.
        ///     Sign-up returns pending users regardless of this option.
        /// </summary>
        public bool AllowUnconfirmedUserSessions { get; set; }
    }
}
