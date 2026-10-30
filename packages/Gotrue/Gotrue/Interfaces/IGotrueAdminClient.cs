using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Interfaces;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateCustomProvider;
using Supabase.Gotrue.Mfa;
using Supabase.Gotrue.Responses;
using Supabase.Gotrue.UpdateCustomProvider;
using Supabase.Gotrue.UpdateFactor;

namespace Supabase.Gotrue.Interfaces;

/// <summary>
/// Interface for the Gotrue Admin Client (auth).
/// </summary>
/// <typeparam name="TUser"></typeparam>
public interface IGotrueAdminClient<TUser> : IGettableHeaders
    where TUser : User
{
    /// <summary>
    /// Creates a user using the admin key (not the anonymous key).
    /// Used in trusted server environments, not client apps.
    /// </summary>
    /// <param name="attributes"></param>
    /// <returns></returns>
    Task<TUser?> CreateUser(AdminUserAttributes attributes);

    /// <summary>
    /// Creates a user using the admin key (not the anonymous key).
    /// Used in trusted server environments, not client apps.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="attributes"></param>
    /// <returns></returns>
    Task<TUser?> CreateUser(string email, string password, AdminUserAttributes? attributes = null);

    /// <summary>
    /// Deletes a user using the admin key (not the anonymous key).
    /// Used in trusted server environments, not client apps.
    /// </summary>
    /// <param name="uid">The user uid you want to remove.</param>
    /// <param name="shouldSoftDelete">When true, the user is soft-deleted (identifiable by the hashed user id but not reversible) instead of hard-deleted. Defaults to a hard delete.</param>
    Task<bool> DeleteUser(string uid, bool shouldSoftDelete = false);

    /// <summary>
    /// Gets a user from a user's JWT. This is using the GoTrue server to validate a user's JWT.
    /// </summary>
    /// <param name="jwt"></param>
    /// <returns></returns>
    Task<TUser?> GetUser(string jwt);

    /// <summary>
    /// Signs a user out on the server from their access token, revoking the refresh tokens of the sessions in
    /// <paramref name="scope" />. Access tokens already issued stay valid until they expire. Call it from a server,
    /// for example to end a user's sessions after a password reset or a ban.
    /// </summary>
    /// <param name="accessToken">The user's access token. It authenticates the request: the service key is not sent.</param>
    /// <param name="scope">
    /// Which sessions to revoke: <see cref="Constants.SignOutScope.Global" /> (the default) revokes every session of the
    /// user, <see cref="Constants.SignOutScope.Local" /> only the token's session, and
    /// <see cref="Constants.SignOutScope.Others" /> every session but the token's.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException"><paramref name="accessToken" /> is null, empty or whitespace. Nothing is sent.</exception>
    /// <exception cref="Exceptions.GotrueException">
    /// The server rejected the token. <see cref="Exceptions.GotrueException.ErrorCode" /> is <c>session_not_found</c>
    /// when the token's session was already revoked and <c>user_not_found</c> when its user was deleted;
    /// <see cref="Exceptions.GotrueException.Reason" /> is <see cref="Exceptions.FailureHint.Reason.AdminTokenRequired" />
    /// (error code <c>bad_jwt</c>) when the token is expired, malformed or not a user's token.
    /// </exception>
    /// <example>
    /// <code>
    /// var admin = new AdminClient(serviceRoleKey, new ClientOptions { Url = authUrl, Headers = { ["apikey"] = anonKey } });
    /// await admin.SignOutAsync(userAccessToken, Constants.SignOutScope.Global);
    /// </code>
    /// </example>
    Task SignOutAsync(string accessToken, Constants.SignOutScope scope = Constants.SignOutScope.Global, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by ID from the server using the admin key (not the anonymous key).
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<TUser?> GetUserById(string userId);

    /// <summary>
    /// Sends an email to the user.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task<bool> InviteUserByEmail(string email, InviteUserByEmailOptions? options = null);

    /// <summary>
    /// Lists users
    /// </summary>
    /// <param name="filter">A string for example part of the email</param>
    /// <param name="sortBy">Snake case string of the given key, currently only created_at is supported</param>
    /// <param name="sortOrder">asc or desc, if null desc is used</param>
    /// <param name="page">page to show for pagination</param>
    /// <param name="perPage">items per page for pagination</param>
    /// <returns></returns>
    Task<UserList<TUser>?> ListUsers(string? filter = null, string? sortBy = null, Constants.SortOrder sortOrder = Constants.SortOrder.Descending, int? page = null,
        int? perPage = null);

    /// <summary>
    /// Updates a User using the service key
    /// </summary>
    /// <param name="attributes"></param>
    /// <returns></returns>
    Task<User?> Update(UserAttributes attributes);

    /// <summary>
    /// Update user by Id
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="userData"></param>
    /// <returns></returns>
    Task<User?> UpdateUserById(string userId, AdminUserAttributes userData);

    /// <summary>
    /// Generates email links and OTPs to be sent via a custom email provider.
    /// </summary>
    /// <param name="options">Options for this call. `Password` is required for <see cref="GenerateLinkOptions.LinkType.SignUp"/>, `Data` is an optional parameter for <see cref="GenerateLinkOptions.LinkType.SignUp"/>.</param>
    /// <returns></returns>
    Task<GenerateLinkResponse?> GenerateLink(GenerateLinkOptions options);

    /// <summary>
    /// Lists all factors associated to a specific user.
    /// </summary>
    /// <param name="listFactorParams">A <see cref="MfaAdminListFactorsParams"/> object that contains the user id.</param>
    /// <returns>A list of <see cref="Factor"/> that this user has enabled.</returns>
    Task<MfaAdminListFactorsResponse?> ListFactors(MfaAdminListFactorsParams listFactorsParams);

    /// <summary>
    /// Deletes a factor on a user. This will log the user out of all active sessions if the deleted factor was verified.
    /// </summary>
    /// <param name="listFactorParams">A <see cref="MfaAdminListFactorsParams"/> object that contains the user id.</param>
    /// <returns>A <see cref="MfaAdminDeleteFactorResponse"/> containing the deleted factor id.</returns>
    Task<MfaAdminDeleteFactorResponse?> DeleteFactor(MfaAdminDeleteFactorParams deleteFactorParams);

    /// <summary>
    /// Updates a user's MFA factor, such as its friendly name or the phone number of a phone factor. Only the members set
    /// on <paramref name="changes" /> are sent; the others keep their current value. Requires a full-access API key, such
    /// as the service_role key: call it from a server, never from a client app.
    /// </summary>
    /// <param name="userId">The id of the user who owns the factor, a UUID such as <c>8d3f1c2a-5b6e-4f70-9a81-b2c3d4e5f607</c>.</param>
    /// <param name="factorId">The id of the factor, a UUID such as <c>2b306a77-21dc-4110-ba71-537cb56b9e98</c>.</param>
    /// <param name="changes">
    /// The changes. An empty or blank value cannot clear a setting: the server reads it as unchanged. The server ignores
    /// <see cref="FactorChanges.Phone" /> on a factor that is not a phone factor.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated factor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId" />, <paramref name="factorId" /> or <paramref name="changes" /> is null. Nothing is sent.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="userId" /> or <paramref name="factorId" /> is not a UUID in its canonical, hyphenated form. Nothing is sent.
    /// </exception>
    /// <exception cref="Exceptions.GotrueException">
    /// The server rejected the request. <see cref="Exceptions.GotrueException.ErrorCode" /> is <c>user_not_found</c> when
    /// no user has <paramref name="userId" />, <c>mfa_factor_not_found</c> when the user owns no factor with
    /// <paramref name="factorId" />, and <c>validation_failed</c> when the phone number is not in E.164 format;
    /// <see cref="Exceptions.GotrueException.Reason" /> is <see cref="Exceptions.FailureHint.Reason.AdminTokenRequired" />
    /// when the key is not a full-access key. A friendly name another factor of the user already has fails with
    /// status code 500.
    /// </exception>
    /// <example>
    /// <code>
    /// using Supabase.Gotrue.UpdateFactor;
    ///
    /// var admin = new AdminClient(serviceRoleKey, new ClientOptions { Url = authUrl });
    /// var factor = await admin.UpdateFactorAsync(userId, factorId, new FactorChanges { FriendlyName = "Work phone" });
    /// </code>
    /// </example>
    Task<Factor> UpdateFactorAsync(string userId, string factorId, FactorChanges changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a custom OAuth/OIDC provider. Requires a full-access API key, such as the service_role key:
    /// call it from a server, never from a client app.
    /// </summary>
    /// <param name="identifier">The provider identifier, starting with <c>custom:</c> (for example <c>custom:mycompany</c>).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">
    /// The identifier lacks the <c>custom:</c> prefix or contains <c>/</c>, <c>\</c>, <c>?</c>, <c>#</c> or <c>%</c>.
    /// Nothing is sent.
    /// </exception>
    /// <exception cref="Exceptions.GotrueException">
    /// The server rejected the request. <see cref="Exceptions.GotrueException.Reason" /> is
    /// <see cref="Exceptions.FailureHint.Reason.CustomProviderNotFound" /> when no provider has this identifier;
    /// <see cref="Exceptions.GotrueException.ErrorCode" /> is <c>feature_disabled</c> when custom providers are disabled on the project.
    /// </exception>
    /// <example>
    /// <code>
    /// var admin = new AdminClient(serviceRoleKey, new ClientOptions { Url = authUrl });
    /// await admin.DeleteCustomProviderAsync("custom:mycompany");
    /// </code>
    /// </example>
    Task DeleteCustomProviderAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a custom OAuth/OIDC provider users can sign in with. Requires a full-access API key, such as the
    /// service_role key: call it from a server, never from a client app.
    /// </summary>
    /// <param name="provider">
    /// The provider to register, built with <see cref="NewCustomProvider.OAuth2" /> or <see cref="NewCustomProvider.Oidc" />.
    /// </param>
    /// <param name="options">Optional settings, such as scopes or attribute mapping. The server applies its defaults to those left null.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The registered provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider" /> is null. Nothing is sent.</exception>
    /// <exception cref="Exceptions.GotrueException">
    /// The server rejected the request. <see cref="Exceptions.GotrueException.ErrorCode" /> is <c>conflict</c> when
    /// a provider already has this identifier, <c>over_custom_provider_quota</c> when the project's provider limit
    /// is reached, <c>validation_failed</c> when a setting is invalid (for example a URL that is not HTTPS, or an
    /// OIDC issuer whose discovery document cannot be fetched), and <c>feature_disabled</c> when custom providers
    /// are disabled on the project. <see cref="Exceptions.GotrueException.Reason" /> is
    /// <see cref="Exceptions.FailureHint.Reason.AdminTokenRequired" /> when the key is not a full-access key.
    /// </exception>
    /// <example>
    /// <code>
    /// using Supabase.Gotrue.Common;
    /// using Supabase.Gotrue.CreateCustomProvider;
    ///
    /// var admin = new AdminClient(serviceRoleKey, new ClientOptions { Url = authUrl });
    /// var provider = await admin.CreateCustomProviderAsync(
    ///     NewCustomProvider.Oidc("custom:mycompany", "My Company SSO", clientId: "id", clientSecret: "secret",
    ///         issuer: new Uri("https://accounts.mycompany.com")),
    ///     new CustomProviderOptions { Scopes = new[] { "email", "profile" } });
    /// </code>
    /// </example>
    Task<CustomProvider> CreateCustomProviderAsync(NewCustomProvider provider, CustomProviderOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a custom OAuth/OIDC provider. Only the members set on <paramref name="changes" /> are sent; the others
    /// keep their current value. Requires a full-access API key, such as the service_role key: call it from a server,
    /// never from a client app.
    /// </summary>
    /// <param name="identifier">The provider identifier, starting with <c>custom:</c> (for example <c>custom:mycompany</c>).</param>
    /// <param name="changes">The changes. An empty or blank value cannot clear a setting: the server reads it as unchanged.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="changes" /> is null. Nothing is sent.</exception>
    /// <exception cref="ArgumentException">
    /// The identifier lacks the <c>custom:</c> prefix or contains <c>/</c>, <c>\</c>, <c>?</c>, <c>#</c> or <c>%</c>.
    /// Nothing is sent.
    /// </exception>
    /// <exception cref="Exceptions.GotrueException">
    /// The server rejected the request. <see cref="Exceptions.GotrueException.Reason" /> is
    /// <see cref="Exceptions.FailureHint.Reason.CustomProviderNotFound" /> when no provider has this identifier and
    /// <see cref="Exceptions.FailureHint.Reason.AdminTokenRequired" /> when the key is not a full-access key;
    /// <see cref="Exceptions.GotrueException.ErrorCode" /> is <c>validation_failed</c> when a setting is invalid (for
    /// example a URL that is not HTTPS, or an OIDC issuer whose discovery document cannot be fetched) and
    /// <c>feature_disabled</c> when custom providers are disabled on the project.
    /// </exception>
    /// <example>
    /// <code>
    /// using Supabase.Gotrue.Common;
    /// using Supabase.Gotrue.UpdateCustomProvider;
    ///
    /// var admin = new AdminClient(serviceRoleKey, new ClientOptions { Url = authUrl });
    /// var updated = await admin.UpdateCustomProviderAsync("custom:mycompany", new CustomProviderChanges
    /// {
    ///     Name = "My Company",
    ///     Options = new CustomProviderOptions { Enabled = false },
    /// });
    /// </code>
    /// </example>
    Task<CustomProvider> UpdateCustomProviderAsync(string identifier, CustomProviderChanges changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a custom OAuth/OIDC provider by its identifier. Requires a full-access API key, such as the
    /// service_role key: call it from a server, never from a client app.
    /// </summary>
    /// <param name="identifier">The provider identifier, starting with <c>custom:</c> (for example <c>custom:mycompany</c>).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The provider. Its client secret is never returned.</returns>
    /// <exception cref="ArgumentException">
    /// The identifier lacks the <c>custom:</c> prefix or contains <c>/</c>, <c>\</c>, <c>?</c>, <c>#</c> or <c>%</c>.
    /// Nothing is sent.
    /// </exception>
    /// <exception cref="Exceptions.GotrueException">
    /// The server rejected the request. <see cref="Exceptions.GotrueException.Reason" /> is
    /// <see cref="Exceptions.FailureHint.Reason.CustomProviderNotFound" /> when no provider has this identifier and
    /// <see cref="Exceptions.FailureHint.Reason.AdminTokenRequired" /> when the key is not a full-access key;
    /// <see cref="Exceptions.GotrueException.ErrorCode" /> is <c>feature_disabled</c> when custom providers are disabled on the project.
    /// </exception>
    /// <example>
    /// <code>
    /// var admin = new AdminClient(serviceRoleKey, new ClientOptions { Url = authUrl });
    /// var provider = await admin.GetCustomProviderAsync("custom:mycompany");
    /// </code>
    /// </example>
    Task<CustomProvider> GetCustomProviderAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the custom OAuth/OIDC providers registered on the project, optionally only those of one type.
    /// Requires a full-access API key, such as the service_role key: call it from a server, never from a client app.
    /// </summary>
    /// <param name="type">Only providers of this type; every provider when null.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The providers, empty when none is registered. Their client secrets are never returned.</returns>
    /// <exception cref="Exceptions.GotrueException">
    /// The server rejected the request. <see cref="Exceptions.GotrueException.Reason" /> is
    /// <see cref="Exceptions.FailureHint.Reason.AdminTokenRequired" /> when the key is not a full-access key;
    /// <see cref="Exceptions.GotrueException.ErrorCode" /> is <c>feature_disabled</c> when custom providers are disabled on the project.
    /// </exception>
    /// <example>
    /// <code>
    /// using Supabase.Gotrue.Common;
    ///
    /// var admin = new AdminClient(serviceRoleKey, new ClientOptions { Url = authUrl });
    /// var oidcProviders = await admin.ListCustomProvidersAsync(CustomProviderType.Oidc);
    /// </code>
    /// </example>
    Task<IReadOnlyList<CustomProvider>> ListCustomProvidersAsync(CustomProviderType? type = null, CancellationToken cancellationToken = default);
}
