using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase.Core.Http;
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue.Mfa;
using Supabase.Gotrue.Responses;

namespace Supabase.Gotrue;

/// <summary>
/// Admin client for interacting with the Gotrue API. Intended for use on
/// servers or other secure environments.
///
/// This client does NOT manage user sessions or track any other state.
/// </summary>
public class AdminClient : IGotrueAdminClient<User>
{
    /// <summary>
    /// The initialized client options.
    /// </summary>
    public ClientOptions Options { get; }

    /// <summary>
    /// Initialize the client with a service key. 
    /// </summary>
    /// <param name="serviceKey">A valid JWT. Must be a full-access API key (e.g. 'service_role' or 'supabase_admin'). </param>
    /// <param name="options"></param>
    public AdminClient(string serviceKey, ClientOptions? options = null)
    {
        this.serviceKey = serviceKey;

        options ??= new ClientOptions();
        this.Options = options;
        this.api = new Api(options.Url, options.Headers, options.HttpClient ?? (options.Proxy != null ? DefaultHttpClientFactory.Create(proxy: options.Proxy) : null), options.Retry);
    }

    /// <summary>
    /// Headers sent to the API on every request.
    /// </summary>
    public Func<Dictionary<string, string>>? GetHeaders
    {
        get => this.api.GetHeaders;
        set => this.api.GetHeaders = value;
    }

    /// <summary>
    /// The underlying API requests object that sends the requests
    /// </summary>
    private readonly IGotrueApi<User, Session> api;

    /// <summary>
    /// The service key used to authenticate with the API.
    /// </summary>
    private readonly string serviceKey;

    /// <inheritdoc />
    public Task<User?> GetUserById(string userId) => this.api.GetUserById(this.serviceKey, userId);

    /// <inheritdoc />
    public Task<User?> GetUser(string jwt) => this.api.GetUser(jwt);

    /// <inheritdoc />
    public async Task<bool> InviteUserByEmail(string email, InviteUserByEmailOptions? options = null)
    {
        var response = await this.api.InviteUserByEmail(email, this.serviceKey, options);
        response.ResponseMessage?.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteUser(string uid, bool shouldSoftDelete = false)
    {
        var result = await this.api.DeleteUser(uid, this.serviceKey, shouldSoftDelete);
        result.ResponseMessage?.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public Task<User?> CreateUser(string email, string password, AdminUserAttributes? attributes = null)
    {
        attributes ??= new AdminUserAttributes();
        attributes.Email = email;
        attributes.Password = password;

        return this.CreateUser(attributes);
    }

    /// <inheritdoc />
    public Task<User?> CreateUser(AdminUserAttributes attributes) => this.api.CreateUser(this.serviceKey, attributes);

    /// <inheritdoc />
    public Task<UserList<User>?> ListUsers(string? filter = null, string? sortBy = null, Constants.SortOrder sortOrder = Constants.SortOrder.Descending, int? page = null, int? perPage = null) => this.api.ListUsers(this.serviceKey, filter, sortBy, sortOrder, page, perPage);

    /// <inheritdoc />
    public Task<User?> UpdateUserById(string userId, AdminUserAttributes userData) => this.api.UpdateUserById(this.serviceKey, userId, userData);

    /// <inheritdoc />
    public async Task<GenerateLinkResponse?> GenerateLink(GenerateLinkOptions options)
    {
        var response = await this.api.GenerateLink(this.serviceKey, options);
        response.ResponseMessage?.EnsureSuccessStatusCode();

        if (response.Content is null)
            return null;

        var result = JsonSerializer.Deserialize<GenerateLinkResponse>(response.Content, Helpers.SerializerOptions);
        return result;
    }

    /// <inheritdoc />
    public async Task<MfaAdminListFactorsResponse?> ListFactors(MfaAdminListFactorsParams listFactorsParams)
    {
        var response = await this.api.ListFactors(this.serviceKey, listFactorsParams);
        response.ResponseMessage?.EnsureSuccessStatusCode();

        if (response.Content is null)
            return null;

        var result = JsonSerializer.Deserialize<List<Factor>>(response.Content, Helpers.SerializerOptions);
        var listFactorsResponse = new MfaAdminListFactorsResponse
        {
            Factors = result
        };

        return listFactorsResponse;
    }

    public async Task<MfaAdminDeleteFactorResponse?> DeleteFactor(MfaAdminDeleteFactorParams deleteFactorParams) => await this.api.DeleteFactor(this.serviceKey, deleteFactorParams);

    /// <inheritdoc />
    public async Task<User?> Update(UserAttributes attributes)
    {
        var result = await this.api.UpdateUser(this.serviceKey, attributes);
        return result;
    }
}
