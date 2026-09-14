#region

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Supabase.Core.Diagnostics;
using Supabase.Core.Http;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue.Mfa;
using static Supabase.Gotrue.Constants;
using static Supabase.Gotrue.Constants.AuthState;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Supabase.Gotrue;

/// <inheritdoc />
public class Client : IGotrueClient<User, Session>
{
    /// <summary>
    ///     The underlying API requests object that sends the requests
    /// </summary>
    private readonly IGotrueApi<User, Session> api;

    /// <summary>
    ///     Handlers for notifications of state changes.
    /// </summary>
    private readonly List<IGotrueClient<User, Session>.AuthEventHandler> authEventHandlers =
        new List<IGotrueClient<User, Session>.AuthEventHandler>();

    /// <summary>
    ///     Gets notifications if there is a failure not visible by exceptions (e.g. background thread refresh failure)
    /// </summary>
#pragma warning disable CS0618 // internal plumbing for the obsolete debug surface, removed together in v8
    private DebugNotification? debugNotification;
#pragma warning restore CS0618

    /// <summary>
    ///     Object called to persist the session (e.g. filesystem or cookie)
    /// </summary>
    private IGotruePersistenceListener<Session>? sessionPersistence;

    /// <summary>
    ///     Guards <see cref="refreshAttempt" /> and the writes to <see cref="CurrentSession" />.
    /// </summary>
    private readonly object refreshGate = new();

    /// <summary>
    ///     Orders persistence writes, so a destroy in flight cannot land after the save that replaced it.
    /// </summary>
    private readonly SemaphoreSlim persistenceGate = new(1, 1);

    /// <summary>
    ///     The running token refresh, shared by concurrent callers. A completed attempt is replaced, never reused.
    /// </summary>
    internal RefreshAttempt? refreshAttempt;

    /// <summary>
    ///     Initializes the GoTrue stateful client.
    ///     You will likely want to at least specify a
    ///     <see>
    ///         <cref>ClientOptions.Url</cref>
    ///     </see>
    ///     Sessions are not automatically retrieved when this object is created.
    ///     If you want to load the session from your persistence store,
    ///     <see>
    ///         <cref>GotrueSessionPersistence</cref>
    ///     </see>
    ///     .
    ///     If you want to load/refresh the session,
    ///     <see>
    ///         <cref>RetrieveSessionAsync</cref>
    ///     </see>
    ///     .
    ///     For a typical client application, you'll want to load the session from persistence
    ///     and then refresh it. If your application is listening for session changes, a valid
    ///     persisted session raises UserUpdated when it is loaded from disk, and TokenRefreshed
    ///     once the refresh succeeds - neither step raises SignedIn.
    ///     <remarks></remarks>
    ///     <example>
    ///         var client = new Supabase.Gotrue.Client(options);
    ///         client.LoadSession();
    ///         await client.RetrieveSessionAsync();
    ///     </example>
    /// </summary>
    /// <param name="options"></param>
    public Client(ClientOptions? options = null)
    {
        options ??= new ClientOptions();
        this.Options = options;
        this.api = new Api(options.Url, options.Headers, options.HttpClient ?? (options.Proxy != null ? DefaultHttpClientFactory.Create(proxy: options.Proxy) : null), options.Retry);
        if (options.AutoRefreshToken)
        {
            this.TokenRefresh = new TokenRefresh(this);
            this.authEventHandlers.Add(this.TokenRefresh.ManageAutoRefresh);
        }
    }

    /// <summary>
    ///     Get the TokenRefresh object, if it exists
    /// </summary>
    public TokenRefresh? TokenRefresh { get; }

    /// <inheritdoc />
    public void SetPersistence(IGotrueSessionPersistence<Session> persistence) =>
        this.sessionPersistence = new PersistenceListener(persistence);

    /// <inheritdoc />
    public ClientOptions Options { get; }

    /// <inheritdoc />
    public Task<User?> GetUser(string jwt) => this.api.GetUser(jwt);

    /// <inheritdoc />
    public void NotifyAuthStateChange(AuthState stateChanged) => this.InvokeStateChangedHandlers(stateChanged);

    /// <inheritdoc />
    public async Task NotifyAuthStateChangeAsync(AuthState stateChanged, CancellationToken cancellationToken = default)
    {
        await this.PersistSessionChangeAsync(stateChanged, cancellationToken).ConfigureAwait(false);
        this.InvokeStateChangedHandlers(stateChanged);
    }

    private async Task PersistSessionChangeAsync(AuthState stateChanged, CancellationToken cancellationToken)
    {
        if (this.sessionPersistence == null)
        {
            return;
        }
        await this.persistenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A SignedOut that finds a session was raced by a sign-in, and must not wipe what that sign-in saved.
            if (stateChanged == SignedOut && this.CurrentSession != null)
            {
                return;
            }
            await this.sessionPersistence.EventHandlerAsync(this, stateChanged, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this.debugNotification?.Log("Auth State Change Handler Failure", e);
        }
        finally
        {
            this.persistenceGate.Release();
        }
    }

    private void InvokeStateChangedHandlers(AuthState stateChanged)
    {
        foreach (var handler in this.authEventHandlers)
        {
            try
            {
                handler.Invoke(this, stateChanged);
            }
            catch (Exception e)
            {
                this.debugNotification?.Log("Auth State Change Handler Failure", e);
            }
        }
    }

    /// <inheritdoc />
    public User? CurrentUser => this.CurrentSession?.User;

    /// <inheritdoc />
    public void AddStateChangedListener(IGotrueClient<User, Session>.AuthEventHandler authEventHandler)
    {
        if (this.authEventHandlers.Contains(authEventHandler))
        {
            return;
        }
        this.authEventHandlers.Add(authEventHandler);
    }

    /// <inheritdoc />
    public void RemoveStateChangedListener(IGotrueClient<User, Session>.AuthEventHandler authEventHandler)
    {
        if (!this.authEventHandlers.Contains(authEventHandler))
        {
            return;
        }
        this.authEventHandlers.Remove(authEventHandler);
    }

    /// <inheritdoc />
    public void ClearStateChangedListeners() => this.authEventHandlers.Clear();

    /// <inheritdoc />
    public bool Online { get; set; } = true;

    /// <inheritdoc />
    public Session? CurrentSession { get; private set; }

    /// <inheritdoc />
    public Task<Session?> SignUp(string email, string password, SignUpOptions? options = null) =>
        this.SignUp(SignUpType.Email, email, password, options);

    /// <inheritdoc />
    public async Task<Session?> SignUp(SignUpType type, string identifier, string password,
        SignUpOptions? options = null)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SignUp);
        activity?.SetTag(GotrueInstrumentation.Tags.SignUpType, type.ToString());
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var session = type switch
        {
            SignUpType.Email => await this.api.SignUpWithEmail(identifier, password, options),
            SignUpType.Phone => await this.api.SignUpWithPhone(identifier, password, options),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
        if (session?.User?.IsConfirmed == true || session?.User != null && this.Options.AllowUnconfirmedUserSessions)
        {
            await this.UpdateSessionAsync(session).ConfigureAwait(false);
            await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
            return this.CurrentSession;
        }
        return session;
    }

    /// <inheritdoc />
    public async Task<bool> SignIn(string email, SignInOptions? options = null)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SendMagicLink);
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        await this.api.SendMagicLinkEmail(email, options);
        return true;
    }

    /// <inheritdoc />
    public async Task<Session?> SignInWithIdToken(Provider provider, string idToken, string? accessToken = null, string? nonce = null,
        string? captchaToken = null)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SignInWithIdToken);
        activity?.SetTag(GotrueInstrumentation.Tags.Provider, provider.ToString());
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var result = await this.api.SignInWithIdToken(provider, idToken, accessToken, nonce, captchaToken);
        await this.UpdateSessionAsync(result).ConfigureAwait(false);
        await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<PasswordlessSignInState> SignInWithOtp(SignInWithPasswordlessEmailOptions options)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SignInWithOtp);
        activity?.SetTag(GotrueInstrumentation.Tags.OtpChannel, GotrueInstrumentation.Channels.Email);
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        return await this.api.SignInWithOtp(options);
    }

    /// <inheritdoc />
    public async Task<PasswordlessSignInState> SignInWithOtp(SignInWithPasswordlessPhoneOptions options)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SignInWithOtp);
        activity?.SetTag(GotrueInstrumentation.Tags.OtpChannel, GotrueInstrumentation.Channels.Phone);
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        return await this.api.SignInWithOtp(options);
    }

    /// <inheritdoc />
    public Task<bool> SendMagicLink(string email, SignInOptions? options = null) => this.SignIn(email, options);

    /// <inheritdoc />
    public Task<Session?> SignIn(string email, string password) => this.SignIn(SignInType.Email, email, password);

    /// <inheritdoc />
    public Task<Session?> SignInWithPassword(string email, string password) => this.SignIn(email, password);

    /// <inheritdoc />
    public async Task<Session?> SignIn(SignInType type, string identifierOrToken, string? password = null,
        string? scopes = null)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SignIn);
        activity?.SetTag(GotrueInstrumentation.Tags.SignInType, type.ToString());
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        Session? newSession;
        switch (type)
        {
            case SignInType.Email:
                newSession = await this.api.SignInWithEmail(identifierOrToken, password!);
                await this.UpdateSessionAsync(newSession).ConfigureAwait(false);
                break;
            case SignInType.Phone:
                if (string.IsNullOrEmpty(password))
                {
                    await this.api.SendMobileOTP(identifierOrToken);
                    return null;
                }
                newSession = await this.api.SignInWithPhone(identifierOrToken, password!);
                await this.UpdateSessionAsync(newSession).ConfigureAwait(false);
                break;
            case SignInType.RefreshToken:
                if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
                {
                    throw new GotrueException("Not logged in.", NoSessionFound);
                }
                await this.RefreshToken(this.CurrentSession.AccessToken!, identifierOrToken);
                return this.CurrentSession;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        // Handle case when a user registers and has not confirmed email (and options do not allow for this), return null for session.
        if (newSession?.User?.IsConfirmed != true &&
            (newSession?.User == null || !this.Options.AllowUnconfirmedUserSessions))
        {
            return null;
        }
        await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
        return this.CurrentSession;
    }

    /// <inheritdoc />
    public async Task<ProviderAuthState> SignIn(Provider provider, SignInOptions? options = null)
    {
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        return this.api.GetUriForProvider(provider, options);
    }

    /// <inheritdoc />
    public async Task<SSOResponse?> SignInWithSSO(Guid providerId, SignInWithSSOOptions? options = null)
    {
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        return await this.api.SignInWithSSO(providerId, options).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SSOResponse?> SignInWithSSO(string domain, SignInWithSSOOptions? options = null)
    {
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        return await this.api.SignInWithSSO(domain, options).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Session?> SignInAnonymously(SignInAnonymouslyOptions? options = null)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SignInAnonymously);
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var newSession = await this.api.SignInAnonymously(options);
        await this.UpdateSessionAsync(newSession).ConfigureAwait(false);
        await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
        return this.CurrentSession;
    }

    /// <inheritdoc />
    public async Task<Session?> VerifyOTP(string phone, string token, MobileOtpType type = MobileOtpType.SMS)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.VerifyOtp);
        activity?.SetTag(GotrueInstrumentation.Tags.OtpChannel, GotrueInstrumentation.Channels.Phone);
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var session = await this.api.VerifyMobileOTP(phone, token, type);
        if (session?.AccessToken != null)
        {
            await this.UpdateSessionAsync(session).ConfigureAwait(false);
            await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
            return session;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<Session?> VerifyOTP(string email, string token, EmailOtpType type = EmailOtpType.MagicLink)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.VerifyOtp);
        activity?.SetTag(GotrueInstrumentation.Tags.OtpChannel, GotrueInstrumentation.Channels.Email);
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var session = await this.api.VerifyEmailOTP(email, token, type);
        if (session?.AccessToken != null)
        {
            await this.UpdateSessionAsync(session).ConfigureAwait(false);
            await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
            return session;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<Session?> VerifyTokenHash(string tokenHash, EmailOtpType type = EmailOtpType.Email)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.VerifyTokenHash);
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var session = await this.api.VerifyTokenHash(tokenHash, type);
        if (session?.AccessToken != null)
        {
            await this.UpdateSessionAsync(session).ConfigureAwait(false);
            await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
            return session;
        }
        return null;
    }

    /// <inheritdoc />
    public Task<ProviderAuthState> LinkIdentity(Provider provider, SignInOptions options)
    {
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        if (this.CurrentSession == null || this.CurrentUser == null)
        {
            throw new GotrueException("A valid session is required.", NoSessionFound);
        }
        if (options.FlowType != OAuthFlowType.PKCE)
        {
            throw new GotrueException("PKCE flow type is required for this action.", InvalidFlowType);
        }
        return this.api.LinkIdentity(this.CurrentSession.AccessToken!, provider, options);
    }

    /// <inheritdoc />
    public async Task<Session?> LinkIdentityWithIdToken(LinkIdentityWithIdTokenOptions options)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.LinkIdentityWithIdToken);
        activity?.SetTag(GotrueInstrumentation.Tags.Provider, options.Provider.ToString());
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        if (this.CurrentSession == null || this.CurrentUser == null)
        {
            throw new GotrueException("A valid session is required.", NoSessionFound);
        }
        var result = await this.api.LinkIdentityWithIdToken(this.CurrentSession.AccessToken!, options).ConfigureAwait(false);
        if (result?.AccessToken != null)
        {
            await this.UpdateSessionAsync(result).ConfigureAwait(false);
            await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
        }
        return result;
    }

    /// <inheritdoc />
    public Task<bool> UnlinkIdentity(UserIdentity userIdentity)
    {
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        if (this.CurrentSession == null || this.CurrentUser == null)
        {
            throw new GotrueException("A valid session is required.", NoSessionFound);
        }
        return this.api.UnlinkIdentity(this.CurrentSession.AccessToken!, userIdentity);
    }

    /// <inheritdoc />
    public async Task SignOut(SignOutScope scope = SignOutScope.Global)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SignOut);
        activity?.SetTag(GotrueInstrumentation.Tags.SignOutScope, scope.ToString());
        if (this.CurrentSession?.AccessToken != null)
        {
            await this.api.SignOut(this.CurrentSession.AccessToken, scope);
        }
        await this.UpdateSessionAsync(null).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<User?> Update(UserAttributes attributes)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.UpdateUser);
        var session = this.CurrentSession;
        if (session == null || string.IsNullOrEmpty(session.AccessToken))
        {
            throw new GotrueException("Not Logged in.");
        }
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var result = await this.api.UpdateUser(session.AccessToken!, attributes);
        // The session may have refreshed meanwhile, so the update lands on the current one if it is still this user's.
        var current = this.CurrentSession;
        if (current == null || current.User?.Id != result?.Id)
        {
            return result;
        }
        current.User = result;
        await this.NotifyAuthStateChangeAsync(UserUpdated).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> Reauthenticate()
    {
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var response = await this.api.Reauthenticate(this.CurrentSession.AccessToken!);
        return response.ResponseMessage?.IsSuccessStatusCode ?? false;
    }

    /// <inheritdoc />
    public async Task<bool> ResetPasswordForEmail(string email)
    {
        var result = await this.api.ResetPasswordForEmail(email);
        result.ResponseMessage?.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<ResetPasswordForEmailState> ResetPasswordForEmail(ResetPasswordForEmailOptions options)
    {
        var state = await this.api.ResetPasswordForEmail(options);
        return state;
    }

    /// <inheritdoc />
    public async Task<Session?> RefreshSession()
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.RefreshSession);
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        await this.RefreshToken();
        var session = this.CurrentSession;
        if (session == null)
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        session.User = await this.api.GetUser(session.AccessToken);
        return session;
    }

    /// <inheritdoc />
    public async Task<Session> SetSession(string accessToken, string refreshToken, bool forceAccessTokenRefresh = false)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.SetSession);
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            throw new GotrueException("`accessToken` and `refreshToken` cannot be empty.", NoSessionFound);
        }
        var payload = new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Payload;
        if (payload == null || payload.ValidTo == DateTime.MinValue)
        {
            throw new GotrueException("`accessToken`'s payload was of an unknown structure.", NoSessionFound);
        }
        if (payload.ValidTo < DateTime.UtcNow || forceAccessTokenRefresh)
        {
            var result = await this.api.RefreshAccessToken(accessToken, refreshToken);
            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                throw new GotrueException("Could not generate a session given the provided parameters.", NoSessionFound);
            }
            this.SetCurrentSession(result);
            await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
            return result;
        }
        var iat = payload.IssuedAt;
        var exp = payload.ValidTo;
        var expiresIn = (long) (exp - iat).TotalSeconds;
        var session = new Session
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "bearer",
            ExpiresIn = expiresIn,
        };
        // Installed before the user is fetched, so a refresh meanwhile moves to these tokens instead of spending them.
        this.SetCurrentSession(session);
        try
        {
            session.User = await this.api.GetUser(accessToken);
        }
        catch
        {
            // Nothing was announced yet, so the failed install just goes away.
            this.TryReplaceSession(null, refreshToken);
            throw;
        }
        await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
        return session;
    }

    /// <summary>
    ///     Parses a <see cref="Session" /> out of a <see cref="Uri" />'s Query parameters.
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="storeSession"></param>
    /// <returns></returns>
    public async Task<Session?> GetSessionFromUrl(Uri uri, bool storeSession = true)
    {
        var query = string.IsNullOrEmpty(uri.Fragment)
            ? HttpUtility.ParseQueryString(uri.Query)
            : HttpUtility.ParseQueryString('?' + uri.Fragment.TrimStart('#'));
        var errorDescription = query.Get("error_description");
        if (!string.IsNullOrEmpty(errorDescription))
        {
            throw new GotrueException(errorDescription, BadSessionUrl);
        }
        var accessToken = query.Get("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new GotrueException("No access_token detected.", BadSessionUrl);
        }
        var expiresIn = query.Get("expires_in");
        if (string.IsNullOrEmpty(expiresIn))
        {
            throw new GotrueException("No expires_in detected.", BadSessionUrl);
        }
        var refreshToken = query.Get("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new GotrueException("No refresh_token detected.", BadSessionUrl);
        }
        var tokenType = query.Get("token_type");
        if (string.IsNullOrEmpty(tokenType))
        {
            throw new GotrueException("No token_type detected.", BadSessionUrl);
        }
        var user = await this.api.GetUser(accessToken);
        var session = new Session
        {
            AccessToken = accessToken,
            ExpiresIn = long.Parse(expiresIn),
            RefreshToken = refreshToken,
            TokenType = tokenType,
            User = user,
        };
        if (storeSession)
        {
            await this.UpdateSessionAsync(session).ConfigureAwait(false);
            await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
            if (query.Get("type") == "recovery")
            {
                await this.NotifyAuthStateChangeAsync(PasswordRecovery).ConfigureAwait(false);
            }
        }
        return session;
    }

    /// <inheritdoc />
    public async Task<Session?> RetrieveSessionAsync()
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.RetrieveSession);

        // No session, so just return.
        if (this.CurrentSession == null)
        {
            return null;
        }

        // We can't refresh the token offline, so return the session as loaded.
        if (!this.Online)
        {
            return this.CurrentSession;
        }

        // We have a session, and hasn't expired, and we seem to be online. Let's try to refresh it.
        if (this.Options.AutoRefreshToken && this.CurrentSession?.RefreshToken != null)
        {
            try
            {
                await this.RefreshToken();
                return this.CurrentSession;
            }
            catch (GotrueException e) when (e.Reason is InvalidRefreshToken)
            {
                // RefreshToken destroyed the session, unless it was replaced mid-flight.
                activity.SetFailure(e);
                return this.CurrentSession;
            }
            catch (Exception e)
            {
                // Anything else is treated as transient - keep the session so the next refresh can retry.
                // Never log the session itself here - it contains the access and refresh tokens.
                this.debugNotification?.Log($"Failed to refresh token ({e.Message})", e);
                activity.SetFailure(e);
                return this.CurrentSession;
            }
        }
        return this.CurrentSession;
    }

    /// <inheritdoc />
    public async Task<Session?> ExchangeCodeForSession(string codeVerifier, string authCode)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.ExchangeCode);
        var result = await this.api.ExchangeCodeForSession(codeVerifier, authCode);
        if (result != null)
        {
            await this.UpdateSessionAsync(result).ConfigureAwait(false);
            await this.NotifyAuthStateChangeAsync(SignedIn).ConfigureAwait(false);
            return this.CurrentSession;
        }
        return null;
    }

    /// <summary>
    ///     Headers sent to the API on every request.
    /// </summary>
    public Func<Dictionary<string, string>>? GetHeaders
    {
        get => this.api.GetHeaders;
        set => this.api.GetHeaders = value;
    }

    /// <inheritdoc />
    [Obsolete("The debug listener is replaced by OpenTelemetry-compatible diagnostics: subscribe to the ActivitySource and Meter named \"Supabase.Gotrue\". This member will be removed in v8.")]
    public void AddDebugListener(Action<string, Exception?> listener)
    {
#pragma warning disable CS0618 // internal plumbing for the obsolete debug surface, removed together in v8
        this.debugNotification ??= new DebugNotification();
#pragma warning restore CS0618
        this.debugNotification.AddDebugListener(listener);
    }

    /// <inheritdoc />
    public async Task RefreshToken(string accessToken, string refreshToken)
    {
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.RefreshToken);
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            throw new GotrueException("No token provided", NoSessionFound);
        }
        try
        {
            var result = await this.api.RefreshAccessToken(accessToken, refreshToken);
            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                throw new GotrueException("Could not refresh token from provided session.", NoSessionFound);
            }
            this.SetCurrentSession(result);
            await this.NotifyAuthStateChangeAsync(TokenRefreshed).ConfigureAwait(false);
        }
        catch (GotrueException ex) when (ex.Reason is InvalidRefreshToken)
        {
            activity.SetFailure(ex);
            await this.ClearRejectedSessionAsync(refreshToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            activity.SetFailure(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RefreshToken()
    {
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        Task attempt;
        // Refresh tokens are single-use, and startup can race the auto-refresh timer here - so callers share the in-flight attempt.
        // The session is read and the attempt registered under one hold of the gate, so a sign-out cannot interleave.
        lock (this.refreshGate)
        {
            var session = this.CurrentSession;
            if (session == null || string.IsNullOrEmpty(session.AccessToken) || string.IsNullOrEmpty(session.RefreshToken))
            {
                throw new GotrueException("No current session.", NoSessionFound);
            }
            if (this.refreshAttempt is { Refresh.IsCompleted: false } current && current.Token == session.RefreshToken)
            {
                attempt = current.Refresh;
            }
            else
            {
                attempt = this.RefreshCurrentSession(session.AccessToken!, session.RefreshToken!);
                this.refreshAttempt = new RefreshAttempt(session.RefreshToken!, attempt);
            }
        }
        try
        {
            await attempt;
        }
        finally
        {
            // Only this attempt's own registration is cleared, so a later attempt is never deregistered.
            lock (this.refreshGate)
            {
                if (ReferenceEquals(this.refreshAttempt?.Refresh, attempt))
                {
                    this.refreshAttempt = null;
                }
            }
        }
    }

    private async Task RefreshCurrentSession(string accessToken, string refreshToken)
    {
        // The refresh gate is held while this method starts, and app code must never run under it.
        await Task.Yield();
        using var activity = GotrueInstrumentation.Source.StartActivity(GotrueInstrumentation.Spans.RefreshToken);
        try
        {
            var result = await this.api.RefreshAccessToken(accessToken, refreshToken);
            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                throw new GotrueException("Could not refresh token from provided session.", NoSessionFound);
            }
            // The session was replaced while this call was in flight, so the result is the previous user's.
            if (!this.TryReplaceSession(result, refreshToken))
            {
                return;
            }
            await this.NotifyAuthStateChangeAsync(TokenRefreshed).ConfigureAwait(false);
        }
        catch (GotrueException ex) when (ex.Reason is InvalidRefreshToken)
        {
            activity.SetFailure(ex);
            await this.ClearRejectedSessionAsync(refreshToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            // The auto-refresh timer swallows this exception, so mark the span failed to keep it in traces.
            activity.SetFailure(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public void LoadSession()
    {
        if (this.sessionPersistence == null)
        {
            return;
        }
        var before = this.CurrentSession;
        Session? session;
        try
        {
            session = this.sessionPersistence.Persistence.LoadSession();
        }
        catch (Exception e)
        {
            this.debugNotification?.Log($"Failed to load the persisted session ({e.Message})", e);
            return;
        }
        this.ApplyLoadedSession(before, session);
    }

    /// <inheritdoc />
    public async Task LoadSessionAsync(CancellationToken cancellationToken = default)
    {
        if (this.sessionPersistence == null)
        {
            return;
        }
        var before = this.CurrentSession;
        Session? session;
        try
        {
            session = await this.sessionPersistence.Persistence.LoadSessionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this.debugNotification?.Log($"Failed to load the persisted session ({e.Message})", e);
            return;
        }
        this.ApplyLoadedSession(before, session);
    }

    private void ApplyLoadedSession(Session? before, Session? loaded)
    {
        var coldStartWithNothingToRestore = loaded == null && before == null;
        var signInRacedTheLoad = !ReferenceEquals(this.CurrentSession, before);
        if (coldStartWithNothingToRestore || signInRacedTheLoad)
        {
            return;
        }
        if (this.SetCurrentSession(loaded) is { } stateChanged)
        {
            this.NotifyAuthStateChange(stateChanged);
        }
    }

    /// <inheritdoc />
    public Task<Settings?> Settings()
    {
        if (!this.Online)
        {
            return Task.FromResult<Settings?>(null);
        }
        return this.api.Settings();
    }

    /// <inheritdoc />
    public void Debug(string message, Exception? e = null) => this.debugNotification?.Log(message, e);

    /// <inheritdoc />
    public void Shutdown() => this.NotifyAuthStateChange(AuthState.Shutdown);

    /// <inheritdoc />
    public async Task<MfaEnrollResponse?> Enroll(MfaEnrollParams mfaEnrollParams)
    {
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        return await this.api.Enroll(this.CurrentSession.AccessToken, mfaEnrollParams);
    }

    /// <inheritdoc />
    public async Task<MfaChallengeResponse?> Challenge(MfaChallengeParams mfaChallengeParams)
    {
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        return await this.api.Challenge(this.CurrentSession.AccessToken, mfaChallengeParams);
    }

    /// <inheritdoc />
    public async Task<Session?> Verify(MfaVerifyParams mfaVerifyParams)
    {
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var result = await this.api.Verify(this.CurrentSession.AccessToken, mfaVerifyParams);
        if (result == null || string.IsNullOrEmpty(result.AccessToken))
        {
            throw new GotrueException("Could not verify MFA.", MfaChallengeUnverified);
        }
        var session = new Session
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            TokenType = "bearer",
            ExpiresIn = result.ExpiresIn,
            User = result.User,
        };
        await this.UpdateSessionAsync(session).ConfigureAwait(false);
        await this.NotifyAuthStateChangeAsync(MfaChallengeVerified).ConfigureAwait(false);
        return session;
    }

    /// <inheritdoc />
    public async Task<Session?> ChallengeAndVerify(MfaChallengeAndVerifyParams mfaChallengeAndVerifyParams)
    {
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        var challengeResponse = await this.api.Challenge(this.CurrentSession.AccessToken, new MfaChallengeParams
        {
            FactorId = mfaChallengeAndVerifyParams.FactorId,
        });
        if (challengeResponse == null)
        {
            return null;
        }
        var result = await this.api.Verify(this.CurrentSession.AccessToken, new MfaVerifyParams
        {
            FactorId = mfaChallengeAndVerifyParams.FactorId,
            Code = mfaChallengeAndVerifyParams.Code,
            ChallengeId = challengeResponse.Id,
        });
        if (result == null || string.IsNullOrEmpty(result.AccessToken))
        {
            throw new GotrueException("Could not verify MFA.", MfaChallengeUnverified);
        }
        var session = new Session
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            TokenType = "bearer",
            ExpiresIn = result.ExpiresIn,
            User = result.User,
        };
        await this.UpdateSessionAsync(session).ConfigureAwait(false);
        await this.NotifyAuthStateChangeAsync(MfaChallengeVerified).ConfigureAwait(false);
        return session;
    }

    /// <inheritdoc />
    public async Task<MfaUnenrollResponse?> Unenroll(MfaUnenrollParams mfaUnenrollParams)
    {
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        if (!this.Online)
        {
            throw new GotrueException("Only supported when online", Offline);
        }
        return await this.api.Unenroll(this.CurrentSession.AccessToken, mfaUnenrollParams);
    }

    /// <inheritdoc />
    public Task<MfaListFactorsResponse?> ListFactors()
    {
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        var response = new MfaListFactorsResponse
        {
            All = this.CurrentSession.User!.Factors,
            Totp = this.CurrentSession.User!.Factors?.Where(x => x.FactorType == "totp" && x.Status == "verified").ToList(),
        };
        return Task.FromResult(response);
    }

    public Task<MfaGetAuthenticatorAssuranceLevelResponse?> GetAuthenticatorAssuranceLevel()
    {
        if (this.CurrentSession == null || string.IsNullOrEmpty(this.CurrentSession.AccessToken))
        {
            throw new GotrueException("Not Logged in.", NoSessionFound);
        }
        var payload = new JwtSecurityTokenHandler().ReadJwtToken(this.CurrentSession.AccessToken).Payload;
        if (payload == null || payload.ValidTo == DateTime.MinValue)
        {
            throw new GotrueException("`accessToken`'s payload was of an unknown structure.", NoSessionFound);
        }
        AuthenticatorAssuranceLevel? currentLevel = null;
        if (payload.ContainsKey("aal"))
        {
            currentLevel = Enum.TryParse(payload["aal"].ToString(), out AuthenticatorAssuranceLevel parsedLevel) ? parsedLevel : (AuthenticatorAssuranceLevel?) null;
        }
        var nextLevel = currentLevel;
        var verifiedFactors = this.CurrentSession.User!.Factors?.Where(factor => factor.Status == "verified").ToList() ?? new List<Factor>();
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
        return Task.FromResult(response);
    }

    private AuthState? SetCurrentSession(Session? session)
    {
        bool dirty;
        lock (this.refreshGate)
        {
            dirty = this.CurrentSession != session;
            this.CurrentSession = session;
            if (session == null)
            {
                // The session and the in-flight attempt clear together, so a concurrent RefreshToken cannot see one without the other.
                this.refreshAttempt = null;
            }
        }
        if (session == null)
        {
            return SignedOut;
        }
        return dirty ? UserUpdated : null;
    }

    // Writes the session only while the given refresh token is still the current one.
    private bool TryReplaceSession(Session? session, string expectedRefreshToken)
    {
        lock (this.refreshGate)
        {
            if (this.CurrentSession?.RefreshToken != expectedRefreshToken)
            {
                return false;
            }
            this.SetCurrentSession(session);
            return true;
        }
    }

    private async Task UpdateSessionAsync(Session? session, CancellationToken cancellationToken = default)
    {
        if (this.SetCurrentSession(session) is { } stateChanged)
        {
            await this.NotifyAuthStateChangeAsync(stateChanged, cancellationToken).ConfigureAwait(false);
        }
    }

    // Only signs out the session the token belonged to.
    private async Task ClearRejectedSessionAsync(string refreshToken)
    {
        if (!this.TryReplaceSession(null, refreshToken) || this.CurrentSession != null)
        {
            return;
        }
        await this.NotifyAuthStateChangeAsync(SignedOut).ConfigureAwait(false);
    }

    /// <summary>
    ///     A token refresh in flight, with the refresh token it was started for. A caller holding a different
    ///     one belongs to another session, so it starts its own attempt instead of joining this one.
    /// </summary>
    internal readonly struct RefreshAttempt(string token, Task refresh)
    {
        internal string Token { get; } = token;

        internal Task Refresh { get; } = refresh;
    }
}
