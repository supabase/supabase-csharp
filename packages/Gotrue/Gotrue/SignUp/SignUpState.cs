namespace Supabase.Gotrue.SignUp;

/// <summary>
///     The outcome of a sign-up: the session the user is signed in with, or the user awaiting confirmation, and the
///     PKCE verifier to exchange the confirmation link's code with.
/// </summary>
public sealed record SignUpState
{
    /// <summary>
    ///     The session the user is signed in with, when the project does not require confirmation. <c>null</c> while
    ///     the user still has to confirm their email address or phone number.
    /// </summary>
    public Session? Session { get; init; }

    /// <summary>
    ///     The signed-up user. When the identifier already belongs to an account, the server may return an
    ///     obfuscated user whose <see cref="Gotrue.User.Identities" /> is empty. <c>null</c> when the server only
    ///     acknowledged the request.
    /// </summary>
    public User? User { get; init; }

    /// <summary>
    ///     The PKCE code verifier, set for an email sign-up with <see cref="SignInOptions.FlowType" /> set to
    ///     <see cref="Constants.OAuthFlowType.PKCE" />. Pass it with the code from the confirmation link to
    ///     <see cref="Interfaces.IGotrueClient{TUser,TSession}.ExchangeCodeForSession" />.
    /// </summary>
    public string? PKCEVerifier { get; init; }
}
