using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core;
using Supabase.Core.Extensions;
using Supabase.Core.Http;
using Supabase.Gotrue.Claims;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue.Mfa;
using Supabase.Gotrue.Responses;
using static Supabase.Gotrue.Constants;

namespace Supabase.Gotrue;

/// <summary>
/// The REST calls to the Gotrue API.
/// </summary>
public class Api : IGotrueApi<User, Session>
{
    private string Url { get; }

    /// <summary>
    /// Function that can be set to return dynamic headers.
    /// Headers specified in the constructor will ALWAYS take precedence over headers returned by this function.
    /// </summary>
    public Func<Dictionary<string, string>>? GetHeaders { get; set; }

    private Dictionary<string, string> headers;
    /// <summary>
    /// Headers to be sent with every request. These will be merged with any headers returned by GetHeaders.
    /// </summary>
    protected Dictionary<string, string> Headers
    {
        get => this.GetHeaders != null ? this.GetHeaders().MergeLeft(this.headers) : this.headers;
        set
        {
            this.headers = value;

            if (!this.headers.ContainsKey("X-Client-Info"))
                this.headers.Add("X-Client-Info", Util.GetAssemblyVersion(typeof(Client)));
        }
    }

    private readonly HttpClient? httpClient;
    private readonly RetryOptions retry;

    /// <summary>
    /// Creates a new API client
    /// </summary>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="httpClient">The client to send requests through. Defaults to a shared client when null.</param>
    /// <param name="retry">Retry policy applied to each request. Defaults to no retries.</param>
    public Api(string url, Dictionary<string, string>? headers = null, HttpClient? httpClient = null, RetryOptions? retry = null)
    {
        this.Url = url;
        headers ??= new Dictionary<string, string>();
        this.headers = headers;
        this.httpClient = httpClient;
        this.retry = retry ?? new RetryOptions();
    }

    /// <summary>Routes through the resolved <see cref="httpClient"/> and <see cref="retry"/> policy.</summary>
    private Task<BaseResponse> MakeRequestAsync(HttpMethod method, string url, object? data = null, Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) =>
        Helpers.MakeRequestAsync(method, url, data, headers, this.httpClient, this.retry, cancellationToken);

    /// <summary>Routes through the resolved <see cref="httpClient"/> and <see cref="retry"/> policy.</summary>
    private Task<T?> MakeRequestAsync<T>(HttpMethod method, string url, object? data = null, Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) where T : class =>
        Helpers.MakeRequestAsync<T>(method, url, data, headers, this.httpClient, this.retry, cancellationToken);

    /// <summary>
    /// Signs a user up using an email address and password.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="options">Optional Signup data.</param>
    /// <returns></returns>
    public async Task<Session?> SignUpWithEmail(string email, string password, SignUpOptions? options = null)
    {
        var body = new Dictionary<string, object> { { "email", email }, { "password", password } };
        var endpoint = $"{this.Url}/signup";

        if (options != null)
        {
            if (!string.IsNullOrEmpty(options.RedirectTo))
            {
                endpoint = Helpers.AddQueryParams(endpoint, new Dictionary<string, string> { { "redirect_to", options.RedirectTo! } }).ToString();
            }

            if (options.Data != null)
            {
                body.Add("data", options.Data);
            }
        }

        return await this.PostSignUp(endpoint, body).ConfigureAwait(false);
    }

    private async Task<Session?> PostSignUp(string endpoint, Dictionary<string, object> body)
    {
        var response = await this.MakeRequestAsync(HttpMethod.Post, endpoint, body, this.Headers).ConfigureAwait(false);
        return ReadSignUpResponse(response.Content);
    }

    /// <summary>Reads a sign-up response: a session, the bare user returned while confirmation is pending, or null.</summary>
    private static Session? ReadSignUpResponse(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        var session = JsonSerializer.Deserialize<Session>(content, Helpers.SerializerOptions);
        if (session is { User: null })
        {
            // An acknowledgement such as { msg, code } must not become an empty User.
            var user = JsonSerializer.Deserialize<User>(content, Helpers.SerializerOptions);
            session.User = user?.Id != null ? user : null;
            if (session.User == null && session.AccessToken == null)
            {
                return null;
            }
        }

        return session;
    }

    /// <summary>
    /// Logs in an existing user using their email address.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public Task<Session?> SignInWithEmail(string email, string password)
    {
        var body = new Dictionary<string, object> { { "email", email }, { "password", password } };
        return this.MakeRequestAsync<Session>(HttpMethod.Post, $"{this.Url}/token?grant_type=password", body, this.Headers);
    }

    /// <summary>
    /// Log in a user using magiclink or a one-time password (OTP).
    ///
    /// If the `{{ .ConfirmationURL }}` variable is specified in the email template, a magiclink will be sent.
    /// If the `{{ .Token }}` variable is specified in the email template, an OTP will be sent.
    /// If you're using phone sign-ins, only an OTP will be sent. You won't be able to send a magiclink for phone sign-ins.
    ///
    /// Be aware that you may get back an error message that will not distinguish
    /// between the cases where the account does not exist or, that the account
    /// can only be accessed via social login.
    ///
    /// Do note that you will need to configure a Whatsapp sender on Twilio
    /// if you are using phone sign in with the 'whatsapp' channel. The whatsapp
    /// channel is not supported on other providers at this time.
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task<PasswordlessSignInState> SignInWithOtp(SignInWithPasswordlessEmailOptions options)
    {
        var url = string.IsNullOrEmpty(options.EmailRedirectTo) ? $"{this.Url}/otp" : $"{this.Url}/otp?redirect_to={options.EmailRedirectTo}";
        string? verifier = null;

        var body = new Dictionary<string, object>
        {
            { "email", options.Email },
            { "data", options.Data },
            { "create_user", options.ShouldCreateUser }
        };

        if (options.FlowType == OAuthFlowType.PKCE)
        {
            verifier = Helpers.GenerateNonce();
            var challenge = Helpers.GeneratePKCENonceVerifier(verifier);
            body.Add("code_challenge", challenge);
            body.Add("code_challenge_method", "s256");
        }

        if (!string.IsNullOrEmpty(options.CaptchaToken))
            body.Add("gotrue_meta_security", new Dictionary<string, string> { { "captcha_token", options.CaptchaToken! } });

        await this.MakeRequestAsync(HttpMethod.Post, url, body, this.Headers);

        return new PasswordlessSignInState { PKCEVerifier = verifier };
    }

    /// <summary>
    /// Log in a user using magiclink or a one-time password (OTP).
    ///
    /// If the `{{ .ConfirmationURL }}` variable is specified in the email template, a magiclink will be sent.
    /// If the `{{ .Token }}` variable is specified in the email template, an OTP will be sent.
    /// If you're using phone sign-ins, only an OTP will be sent. You won't be able to send a magiclink for phone sign-ins.
    ///
    /// Be aware that you may get back an error message that will not distinguish
    /// between the cases where the account does not exist or, that the account
    /// can only be accessed via social login.
    ///
    /// Do note that you will need to configure a Whatsapp sender on Twilio
    /// if you are using phone sign in with the 'whatsapp' channel. The whatsapp
    /// channel is not supported on other providers at this time.
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task<PasswordlessSignInState> SignInWithOtp(SignInWithPasswordlessPhoneOptions options)
    {
        var url = $"{this.Url}/otp";

        var body = new Dictionary<string, object>
        {
            { "phone", options.Phone },
            { "data", options.Data },
            { "create_user", options.ShouldCreateUser },
            { "channel", Core.Helpers.GetMappedToAttr(options.Channel).Mapping }
        };

        if (!string.IsNullOrEmpty(options.CaptchaToken))
            body.Add("gotrue_meta_security", new Dictionary<string, string> { { "captcha_token", options.CaptchaToken! } });

        await this.MakeRequestAsync(HttpMethod.Post, url, body, this.Headers);

        return new PasswordlessSignInState();
    }

    /// <summary>
    /// Creates a new anonymous user.
    /// </summary>
    /// <param name="options"></param>
    /// <returns>A session where the is_anonymous claim in the access token JWT set to true</returns>
    public async Task<Session?> SignInAnonymously(SignInAnonymouslyOptions? options = null)
    {
        var url = $"{this.Url}/signup";

        var body = new Dictionary<string, object>();

        if (options?.Data != null)
            body.Add("data", options.Data);

        if (options != null && !string.IsNullOrEmpty(options.CaptchaToken))
            body.Add("gotrue_meta_security", new Dictionary<string, string> { { "captcha_token", options.CaptchaToken! } });

        return await this.MakeRequestAsync<Session>(HttpMethod.Post, url, body, this.Headers);
    }

    /// <summary>
    /// Allows signing in with an ID token issued by certain supported providers.
    /// The [idToken] is verified for validity and a new session is established.
    /// This method of signing in only supports [Provider.Google] or [Provider.Apple].
    /// </summary>
    /// <param name="provider">A supported provider (Google, Apple, Azure, Facebook)</param>
    /// <param name="idToken">OIDC ID token issued by the specified provider. The `iss` claim in the ID token must match the supplied provider. Some ID tokens contain an `at_hash` which require that you provide an `access_token` value to be accepted properly. If the token contains a `nonce` claim you must supply the nonce used to obtain the ID token.</param>
    /// <param name="accessToken">If the ID token contains an `at_hash` claim, then the hash of this value is compared to the value in the ID token.</param>
    /// <param name="nonce">If the ID token contains a `nonce` claim, then the hash of this value is compared to the value in the ID token.</param>
    /// <param name="captchaToken">Verification token received when the user completes the captcha on the site.</param>
    /// <returns></returns>
    /// <exception>
    ///     <cref>InvalidProviderException</cref>
    /// </exception>
    public Task<Session?> SignInWithIdToken(Provider provider, string idToken, string? accessToken = null, string? nonce = null, string? captchaToken = null)
    {
        if (provider != Provider.Google && provider != Provider.Apple && provider != Provider.Azure && provider != Provider.Facebook)
            throw new GotrueException($"Provider must be `Google`, `Apple`, `Azure`, or `Facebook` not {provider}");

        var body = new Dictionary<string, object?>
        {
            { "provider", Core.Helpers.GetMappedToAttr(provider).Mapping },
            { "id_token", idToken },
        };

        if (!string.IsNullOrEmpty(accessToken))
            body.Add("access_token", accessToken);

        if (!string.IsNullOrEmpty(nonce))
            body.Add("nonce", nonce);

        if (!string.IsNullOrEmpty(captchaToken))
            body.Add("gotrue_meta_security", new Dictionary<string, object?> { { "captcha_token", captchaToken } });

        return this.MakeRequestAsync<Session>(HttpMethod.Post, $"{this.Url}/token?grant_type=id_token", body, this.Headers);
    }

    private Task<SSOResponse?> SignInWithSsoInternal(Guid? providerId = null, string? domain = null, SignInWithSSOOptions? options = null)
    {
        if (providerId != null && domain != null)
            throw new GotrueException($"Both providerId and domain were provided to the API, " +
                                      $"you must supply either one or the other but not both providerId={providerId}, domain={domain}");
        if (providerId == null && domain == null)
            throw new GotrueException($"Both providerId and domain were null " +
                                      $"you must supply either one or the other but not both providerId={providerId}, domain={domain}");

        string? codeChallenge = null;
        string? codeChallengeMethod = null;
        if (options?.FlowType == OAuthFlowType.PKCE)
        {
            var codeVerifier = Helpers.GenerateNonce();
            codeChallenge = Helpers.GeneratePKCENonceVerifier(codeVerifier);
            codeChallengeMethod = "s256";
        }

        var body = new Dictionary<string, object?>
        {
            {  providerId != null ? "provider_id" : "domain",  providerId != null ? providerId.ToString() : domain},
            { "redirect_to", options?.RedirectTo },

				// this is important, it will not auto redirect the request and instead return the Uri needed to handle the login
				// without this in the body the request will automatically redirect to the providers sign in page
				{ "skip_http_redirect", true },

            { "code_challenge", codeChallenge },
            { "code_challenge_method", codeChallengeMethod }
        };

        if (!string.IsNullOrEmpty(options?.CaptchaToken))
            body.Add("gotrue_meta_security", new Dictionary<string, object?> { { "captcha_token", options?.CaptchaToken } });

        return this.MakeRequestAsync<SSOResponse>(HttpMethod.Post, $"{this.Url}/sso", body, this.Headers);
    }

    /// <inheritdoc />
    public Task<SSOResponse?> SignInWithSSO(Guid providerId, SignInWithSSOOptions? options = null) => this.SignInWithSsoInternal(providerId: providerId, options: options);

    /// <inheritdoc />
    public Task<SSOResponse?> SignInWithSSO(string domain, SignInWithSSOOptions? options = null) => this.SignInWithSsoInternal(domain: domain, options: options);

    /// <summary>
    /// Sends a magic login link to an email address.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public Task<BaseResponse> SendMagicLinkEmail(string email, SignInOptions? options = null)
    {
        var data = new Dictionary<string, string> { { "email", email } };

        var endpoint = $"{this.Url}/magiclink";

        if (options != null)
        {
            if (!string.IsNullOrEmpty(options.RedirectTo))
            {
                endpoint = Helpers.AddQueryParams(endpoint, new Dictionary<string, string> { { "redirect_to", options.RedirectTo! } }).ToString();
            }
        }

        return this.MakeRequestAsync(HttpMethod.Post, endpoint, data, this.Headers);
    }

    /// <summary>
    /// Sends an invite link to an email address.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="jwt">this token needs role 'supabase_admin' or 'service_role'</param>
    /// <param name="options"></param>
    /// <returns></returns>
    public Task<BaseResponse> InviteUserByEmail(string email, string jwt, InviteUserByEmailOptions? options = null)
    {
        var url = options == null || string.IsNullOrEmpty(options.RedirectTo) ? $"{this.Url}/invite" : $"{this.Url}/invite?redirect_to={options.RedirectTo}";
        var body = new Dictionary<string, object> { { "email", email } };

        if (options?.Data != null)
            body["data"] = options.Data;

        return this.MakeRequestAsync(HttpMethod.Post, url, body, this.CreateAuthedRequestHeaders(jwt));
    }

    /// <summary>
    /// Signs up a new user using their phone number and a password.The phone number of the user.
    /// </summary>
    /// <param name="phone">The phone number of the user.</param>
    /// <param name="password">The password of the user.</param>
    /// <param name="options">Optional Signup data.</param>
    /// <returns></returns>
    public Task<Session?> SignUpWithPhone(string phone, string password, SignUpOptions? options = null)
    {
        if (string.IsNullOrEmpty(phone))
            throw new GotrueException("Phone number not provided.", FailureHint.Reason.UserBadPhoneNumber);

        var body = new Dictionary<string, object>
        {
            { "phone", phone },
            { "password", password },
        };

        var endpoint = $"{this.Url}/signup";

        if (options != null)
        {
            if (!string.IsNullOrEmpty(options.RedirectTo))
            {
                endpoint = Helpers.AddQueryParams(endpoint, new Dictionary<string, string> { { "redirect_to", options.RedirectTo! } }).ToString();
            }

            if (options.Data != null)
            {
                body.Add("data", options.Data);
            }
        }

        return this.PostSignUp(endpoint, body);
    }

    /// <summary>
    /// Logs in an existing user using their phone number and password.
    /// </summary>
    /// <param name="phone">The phone number of the user.</param>
    /// <param name="password">The password of the user.</param>
    /// <returns></returns>
    public Task<Session?> SignInWithPhone(string phone, string password)
    {
        var data = new Dictionary<string, object>
        {
            { "phone", phone },
            { "password", password }
        };
        return this.MakeRequestAsync<Session>(HttpMethod.Post, $"{this.Url}/token?grant_type=password", data, this.Headers);
    }

    /// <summary>
    /// Sends a mobile OTP via SMS. Will register the account if it doesn't already exist
    /// </summary>
    /// <param name="phone">phone The user's phone number WITH international prefix</param>
    /// <returns></returns>
    public Task<BaseResponse> SendMobileOTP(string phone)
    {
        var data = new Dictionary<string, string> { { "phone", phone } };
        return this.MakeRequestAsync(HttpMethod.Post, $"{this.Url}/otp", data, this.Headers);
    }

    /// <summary>
    /// Send User supplied Mobile OTP to be verified
    /// </summary>
    /// <param name="phone">The user's phone number WITH international prefix</param>
    /// <param name="token">token that user was sent to their mobile phone</param>
    /// <param name="type">e.g. SMS or phone change</param>
    /// <returns></returns>
    public Task<Session?> VerifyMobileOTP(string phone, string token, MobileOtpType type)
    {
        var data = new Dictionary<string, string>
        {
            { "phone", phone },
            { "token", token },
            { "type", Core.Helpers.GetMappedToAttr(type).Mapping }
        };
        return this.MakeRequestAsync<Session>(HttpMethod.Post, $"{this.Url}/verify", data, this.Headers);
    }

    /// <summary>
    /// Send User supplied Email OTP to be verified
    /// </summary>
    /// <param name="email">The user's email address</param>
    /// <param name="token">token that user was sent to their mobile phone</param>
    /// <param name="type">Type of verification, e.g. invite, recovery, etc.</param>
    /// <returns></returns>
    public Task<Session?> VerifyEmailOTP(string email, string token, EmailOtpType type)
    {
        var data = new Dictionary<string, string>
        {
            { "email", email },
            { "token", token },
            { "type", Core.Helpers.GetMappedToAttr(type).Mapping }
        };
        return this.MakeRequestAsync<Session>(HttpMethod.Post, $"{this.Url}/verify", data, this.Headers);
    }

    /// <summary>
    /// Verify token hash used in an email confirmation link.
    /// </summary>
    /// <param name="tokenHash">The token hash used in an email confirmation link</param>
    /// <param name="type">Type of verification, e.g. email.</param>
    /// <returns></returns>
    public Task<Session?> VerifyTokenHash(string tokenHash, EmailOtpType type)
    {
        var data = new Dictionary<string, string>
        {
            { "token_hash", tokenHash },
            { "type", Core.Helpers.GetMappedToAttr(type).Mapping }
        };
        return this.MakeRequestAsync<Session>(HttpMethod.Post, $"{this.Url}/verify", data, this.Headers);
    }

    /// <summary>
    /// Sends a reset request to an email address.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    public Task<BaseResponse> ResetPasswordForEmail(string email)
    {
        var data = new Dictionary<string, string> { { "email", email } };
        return this.MakeRequestAsync(HttpMethod.Post, $"{this.Url}/recover", data, this.Headers);
    }

    /// <summary>
    /// Sends a password reset request to an email address.
    ///
    /// This Method supports the PKCE Flow
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task<ResetPasswordForEmailState> ResetPasswordForEmail(ResetPasswordForEmailOptions options)
    {
        var url = string.IsNullOrEmpty(options.RedirectTo) ? $"{this.Url}/recover" : $"{this.Url}/recover?redirect_to={options.RedirectTo}";
        string? verifier = null;

        var body = new Dictionary<string, object>
        {
            { "email", options.Email },
        };

        if (options.FlowType == OAuthFlowType.PKCE)
        {
            verifier = Helpers.GenerateNonce();
            var challenge = Helpers.GeneratePKCENonceVerifier(verifier);

            body.Add("code_challenge", challenge);
            body.Add("code_challenge_method", "s256");
        }

        if (!string.IsNullOrEmpty(options.CaptchaToken))
            body.Add("gotrue_meta_security", new Dictionary<string, string> { { "captcha_token", options.CaptchaToken! } });

        await this.MakeRequestAsync(HttpMethod.Post, url, body, this.Headers);

        return new ResetPasswordForEmailState { PKCEVerifier = verifier };
    }

    /// <summary>
    /// Create a temporary object with all configured headers and adds the Authorization token to be used on request methods
    /// </summary>
    /// <param name="jwt">JWT</param>
    /// <returns></returns>
    private Dictionary<string, string> CreateAuthedRequestHeaders(string jwt)
    {
        var headers = new Dictionary<string, string>(this.Headers)
        {
            ["Authorization"] = $"Bearer {jwt}"
        };

        // Opaque keys need an apikey header too. Keep any configured key and never copy a JWT here (#424).
        var hasApiKey = headers.Keys.Any(key => string.Equals(key, "apikey", StringComparison.OrdinalIgnoreCase));
        if (!hasApiKey && !new JwtSecurityTokenHandler().CanReadToken(jwt))
            headers["apikey"] = jwt;

        return headers;
    }

    /// <inheritdoc />
    public ProviderAuthState GetUriForProvider(Provider provider, SignInOptions? options = null) =>
        Helpers.GetUrlForProvider($"{this.Url}/authorize", provider, options);

    /// <summary>
    /// Log in an existing user via code from third-party provider.
    /// </summary>
    /// <param name="codeVerifier">Generated verifier (probably from GetUrlForProvider)</param>
    /// <param name="authCode">The received Auth Code Callback</param>
    /// <returns></returns>
    public Task<Session?> ExchangeCodeForSession(string codeVerifier, string authCode)
    {
        var url = new UriBuilder($"{this.Url}/token?grant_type=pkce");
        var body = new Dictionary<string, object>
        {
            { "auth_code", authCode },
            { "code_verifier", codeVerifier }
        };

        return this.MakeRequestAsync<Session>(HttpMethod.Post, url.ToString(), body, this.Headers);
    }

    /// <inheritdoc />
    public Task<MfaEnrollResponse?> Enroll(string jwt, MfaEnrollParams mfaEnrollParams)
    {
        var body = new Dictionary<string, object>
        {
            { "friendly_name", mfaEnrollParams.FriendlyName },
            { "factor_type", mfaEnrollParams.FactorType },
            { "issuer", mfaEnrollParams.Issuer }
        };

        return this.MakeRequestAsync<MfaEnrollResponse>(HttpMethod.Post, $"{this.Url}/factors", body, this.CreateAuthedRequestHeaders(jwt));
    }

    /// <inheritdoc />
    public Task<MfaChallengeResponse?> Challenge(string jwt, MfaChallengeParams mfaChallengeParams) => this.MakeRequestAsync<MfaChallengeResponse>(HttpMethod.Post, $"{this.Url}/factors/{mfaChallengeParams.FactorId}/challenge", null, this.CreateAuthedRequestHeaders(jwt));

    /// <inheritdoc />
    public Task<MfaVerifyResponse?> Verify(string jwt, MfaVerifyParams mfaVerifyParams)
    {
        var body = new Dictionary<string, object>
        {
            { "code", mfaVerifyParams.Code },
            { "challenge_id", mfaVerifyParams.ChallengeId }
        };

        return this.MakeRequestAsync<MfaVerifyResponse>(HttpMethod.Post, $"{this.Url}/factors/{mfaVerifyParams.FactorId}/verify", body, this.CreateAuthedRequestHeaders(jwt));
    }

    /// <inheritdoc />
    public Task<MfaUnenrollResponse?> Unenroll(string jwt, MfaUnenrollParams mfaUnenrollParams) => this.MakeRequestAsync<MfaUnenrollResponse>(HttpMethod.Delete, $"{this.Url}/factors/{mfaUnenrollParams.FactorId}", null, this.CreateAuthedRequestHeaders(jwt));

    /// <inheritdoc />
    public Task<BaseResponse> ListFactors(string jwt, MfaAdminListFactorsParams listFactorsParams) => this.MakeRequestAsync(HttpMethod.Get, $"{this.Url}/admin/users/{listFactorsParams.UserId}/factors", null, this.CreateAuthedRequestHeaders(jwt));

    /// <inheritdoc />
    public Task<MfaAdminDeleteFactorResponse?> DeleteFactor(string jwt, MfaAdminDeleteFactorParams deleteFactorParams) => this.MakeRequestAsync<MfaAdminDeleteFactorResponse>(HttpMethod.Delete, $"{this.Url}/admin/users/{deleteFactorParams.UserId}/factors/{deleteFactorParams.Id}", null, this.CreateAuthedRequestHeaders(jwt));

    /// <inheritdoc />
    public async Task<ProviderAuthState> LinkIdentity(string token, Provider provider, SignInOptions options)
    {
        var state = Helpers.GetUrlForProvider($"{this.Url}/user/identities/authorize", provider, options);

        // Need to skip the HTTP redirect to avoid "No API key found in request" error and match auth-js behavior
        var uri = Helpers.AddQueryParams(state.Uri.ToString(), new Dictionary<string, string> { { "skip_http_redirect", "true" } });
        var response = await this.MakeRequestAsync(HttpMethod.Get, uri.ToString(), null, this.CreateAuthedRequestHeaders(token));

        var content = response?.Content;

        Dictionary<string, string>? payload = null;
        if (!string.IsNullOrEmpty(content))
            payload = JsonSerializer.Deserialize<Dictionary<string, string>>(content);

        if (payload == null || !payload.TryGetValue("url", out var url) || string.IsNullOrEmpty(url))
            throw new GotrueException("Gotrue did not return a provider authorization url for the identity link.", FailureHint.Reason.BadSessionUrl);

        state.Uri = new Uri(url);

        return state;
    }

    /// <inheritdoc />
    public Task<Session?> LinkIdentityWithIdToken(string token, LinkIdentityWithIdTokenOptions options)
    {
        EnsureIdTokenProviderSupported(options.Provider);
        var body = BuildIdTokenLinkBody(options);
        return this.MakeRequestAsync<Session>(HttpMethod.Post, $"{this.Url}/token?grant_type=id_token", body, this.CreateAuthedRequestHeaders(token));
    }

    private static void EnsureIdTokenProviderSupported(Provider provider)
    {
        if (provider != Provider.Google && provider != Provider.Apple && provider != Provider.Azure && provider != Provider.Facebook)
            throw new GotrueException($"Provider must be `Google`, `Apple`, `Azure`, or `Facebook` not {provider}");
    }

    private static Dictionary<string, object?> BuildIdTokenLinkBody(LinkIdentityWithIdTokenOptions options)
    {
        var body = new Dictionary<string, object?>
        {
            { "provider", Core.Helpers.GetMappedToAttr(options.Provider).Mapping },
            { "id_token", options.IdToken },
            { "link_identity", true }
        };

        if (!string.IsNullOrEmpty(options.AccessToken))
            body.Add("access_token", options.AccessToken);

        if (!string.IsNullOrEmpty(options.Nonce))
            body.Add("nonce", options.Nonce);

        if (!string.IsNullOrEmpty(options.CaptchaToken))
            body.Add("gotrue_meta_security", new Dictionary<string, object?> { { "captcha_token", options.CaptchaToken } });

        return body;
    }

    /// <inheritdoc />
    public async Task<bool> UnlinkIdentity(string token, UserIdentity userIdentity)
    {
        var result = await this.MakeRequestAsync(HttpMethod.Delete, $"{this.Url}/user/identities/{userIdentity.IdentityId}", null, this.CreateAuthedRequestHeaders(token));
        return result.ResponseMessage is { IsSuccessStatusCode: true };
    }

    /// <summary>
    /// Removes a logged-in session.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    public Task<BaseResponse> SignOut(string jwt, SignOutScope scope = SignOutScope.Global)
    {
        var data = new Dictionary<string, string>();

        return this.MakeRequestAsync(HttpMethod.Post, $"{this.Url}/logout?scope={Core.Helpers.GetMappedToAttr(scope).Mapping}", data, this.CreateAuthedRequestHeaders(jwt));
    }

    /// <summary>
    /// Gets User Details
    /// </summary>
    /// <param name="jwt"></param>
    /// <returns></returns>
    public Task<User?> GetUser(string jwt) => this.GetUserAsync(jwt);

    /// <summary>
    /// Gets User Details
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<User?> GetUserAsync(string jwt, CancellationToken cancellationToken = default) =>
        this.MakeRequestAsync<User>(HttpMethod.Get, $"{this.Url}/user", null, this.CreateAuthedRequestHeaders(jwt), cancellationToken);

    /// <summary>
    /// Get User details by Id
    /// </summary>
    /// <param name="jwt">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="userId">userID</param>
    /// <returns></returns>
    public Task<User?> GetUserById(string jwt, string userId)
    {
        var data = new Dictionary<string, string>();

        return this.MakeRequestAsync<User>(HttpMethod.Get, $"{this.Url}/admin/users/{userId}", data, this.CreateAuthedRequestHeaders(jwt));
    }

    /// <summary>
    /// Updates the User data
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="attributes"></param>
    /// <returns></returns>
    public Task<User?> UpdateUser(string jwt, UserAttributes attributes) => this.MakeRequestAsync<User>(HttpMethod.Put, $"{this.Url}/user", attributes, this.CreateAuthedRequestHeaders(jwt));

    /// <summary>
    /// Lists users
    /// </summary>
    /// <param name="jwt">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="filter">A string for example part of the email</param>
    /// <param name="sortBy">Snake case string of the given key, currently only created_at is supported</param>
    /// <param name="sortOrder">asc or desc, if null desc is used</param>
    /// <param name="page">page to show for pagination</param>
    /// <param name="perPage">items per page for pagination</param>
    /// <returns></returns>
    public Task<UserList<User>?> ListUsers(string jwt, string? filter = null, string? sortBy = null, SortOrder sortOrder = SortOrder.Descending, int? page = null, int? perPage = null)
    {
        var data = this.TransformListUsersParams(filter, sortBy, sortOrder, page, perPage);

        return this.MakeRequestAsync<UserList<User>>(HttpMethod.Get, $"{this.Url}/admin/users", data, this.CreateAuthedRequestHeaders(jwt));
    }

    private Dictionary<string, string> TransformListUsersParams(string? filter = null, string? sortBy = null, SortOrder sortOrder = SortOrder.Descending, int? page = null, int? perPage = null)
    {
        var query = new Dictionary<string, string>();

        if (filter != null && !string.IsNullOrWhiteSpace(filter))
        {
            query.Add("filter", filter);
        }

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var mapTo = Core.Helpers.GetMappedToAttr(sortOrder);
            query.Add("sort", $"{sortBy} {mapTo.Mapping}");
        }

        if (page.HasValue)
        {
            query.Add("page", page.Value.ToString());
        }

        if (perPage.HasValue)
        {
            query.Add("per_page", perPage.Value.ToString());
        }

        return query;
    }

    /// <summary>
    /// Create a user
    /// </summary>
    /// <param name="jwt">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="attributes">Additional administrative details</param>
    /// <returns></returns>
    public Task<User?> CreateUser(string jwt, AdminUserAttributes? attributes = null)
    {
        attributes ??= new AdminUserAttributes();

        return this.MakeRequestAsync<User>(HttpMethod.Post, $"{this.Url}/admin/users", attributes, this.CreateAuthedRequestHeaders(jwt));
    }

    /// <summary>
    /// Update user by Id
    /// </summary>
    /// <param name="jwt">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="userId">userID</param>
    /// <param name="userData">User attributes e.g. email, password, etc.</param>
    /// <returns></returns>
    public Task<User?> UpdateUserById(string jwt, string userId, UserAttributes userData) => this.MakeRequestAsync<User>(HttpMethod.Put, $"{this.Url}/admin/users/{userId}", userData, this.CreateAuthedRequestHeaders(jwt));

    /// <summary>
    /// Sends a re-authentication request, used for password changes.
    ///
    /// See: https://github.com/supabase/gotrue#get-reauthenticate
    /// </summary>
    /// <param name="userJwt">The user's auth token.</param>
    /// <returns></returns>
    public Task<BaseResponse> Reauthenticate(string userJwt) => this.MakeRequestAsync(HttpMethod.Get, $"{this.Url}/reauthenticate", null, this.CreateAuthedRequestHeaders(userJwt));

    /// <summary>
    /// Delete a user
    /// </summary>
    /// <param name="uid">The user uid you want to remove.</param>
    /// <param name="jwt">A valid JWT. Must be a full-access API key (e.g. service_role key).</param>
    /// <param name="shouldSoftDelete">When true, the user is soft-deleted (identifiable by the hashed user id but not reversible) instead of hard-deleted. Defaults to a hard delete.</param>
    /// <returns></returns>
    public Task<BaseResponse> DeleteUser(string uid, string jwt, bool shouldSoftDelete = false)
    {
        var data = new Dictionary<string, object> { { "should_soft_delete", shouldSoftDelete } };
        return this.MakeRequestAsync(HttpMethod.Delete, $"{this.Url}/admin/users/{uid}", data, this.CreateAuthedRequestHeaders(jwt));
    }

    /// <summary>
    /// Calls the GoTrue server to get the settings (for example, if email auto confirmation is turned on)
    /// </summary>
    /// <returns>mpose up -d
    /// </returns>
    public Task<Settings?> Settings() => this.MakeRequestAsync<Settings>(HttpMethod.Get, $"{this.Url}/settings", null, this.Headers);

    /// <summary>
    /// Gets the server's public keys for verifying JWT signatures.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<Jwks?> GetJwksAsync(CancellationToken cancellationToken = default) =>
        this.MakeRequestAsync<Jwks>(HttpMethod.Get, $"{this.Url}/.well-known/jwks.json", null, this.Headers, cancellationToken);

    /// <summary>
    /// Generates email links and OTPs to be sent via a custom email provider.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public Task<BaseResponse> GenerateLink(string jwt, GenerateLinkOptions options)
    {
        var url = string.IsNullOrEmpty(options.RedirectTo) ? $"{this.Url}/admin/generate_link" : $"{this.Url}/admin/generate_link?redirect_to={options.RedirectTo}";

        return this.MakeRequestAsync(HttpMethod.Post, url, options, this.CreateAuthedRequestHeaders(jwt));
    }

    /// <summary>
    /// Generates a new Session given a user's access token and refresh token.
    /// </summary>
    /// <param name="refreshToken"></param>
    /// <param name="accessToken"></param>
    /// <returns></returns>
    public Task<Session?> RefreshAccessToken(string accessToken, string refreshToken)
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {accessToken}" },
        };

        var data = new Dictionary<string, string>
        {
            { "refresh_token", refreshToken }
        };

        return this.MakeRequestAsync<Session>(HttpMethod.Post, $"{this.Url}/token?grant_type=refresh_token", data, this.Headers.MergeLeft(headers));
    }
}
