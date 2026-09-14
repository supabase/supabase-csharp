using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase.Core.Extensions;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;

namespace Supabase.Postgrest;

/// <inheritdoc />
public class Client : IPostgrestClient
{
    /// <summary>
    /// Custom serializer options (column-name mapping, per-column null handling and the Postgrest
    /// range/date/array converters) used for encoding and decoding Postgrest JSON responses.
    /// </summary>
    public static JsonSerializerOptions SerializerSettings(ClientOptions? options = null)
    {
        options ??= new ClientOptions();
        return PostgrestSerializerOptions.Build(options.SerializeEnumsAsStrings, PostgrestOperation.None);
    }

    /// <inheritdoc />
    public string BaseUrl { get; }

    /// <inheritdoc />
    public ClientOptions Options { get; }

    private readonly HttpClient? httpClient;

    /// <inheritdoc />
    public void AddRequestPreparedHandler(OnRequestPreparedEventHandler handler) =>
        Hooks.Instance.AddRequestPreparedHandler(handler);

    /// <inheritdoc />
    public void RemoveRequestPreparedHandler(OnRequestPreparedEventHandler handler) =>
        Hooks.Instance.AddRequestPreparedHandler(handler);

    /// <inheritdoc />
    public void ClearRequestPreparedHandlers() =>
        Hooks.Instance.ClearRequestPreparedHandlers();

    /// <inheritdoc />
    [Obsolete("The debug handler is replaced by OpenTelemetry-compatible diagnostics: subscribe to the ActivitySource and Meter named \"Supabase.Postgrest\". This member will be removed in a future major version.")]
    public void AddDebugHandler(IPostgrestDebugger.DebugEventHandler handler) =>
        Debugger.Instance.AddDebugHandler(handler);

    /// <inheritdoc />
    [Obsolete("The debug handler is replaced by OpenTelemetry-compatible diagnostics: subscribe to the ActivitySource and Meter named \"Supabase.Postgrest\". This member will be removed in a future major version.")]
    public void RemoveDebugHandler(IPostgrestDebugger.DebugEventHandler handler) =>
        Debugger.Instance.RemoveDebugHandler(handler);

    /// <inheritdoc />
    [Obsolete("The debug handler is replaced by OpenTelemetry-compatible diagnostics: subscribe to the ActivitySource and Meter named \"Supabase.Postgrest\". This member will be removed in a future major version.")]
    public void ClearDebugHandlers() => Debugger.Instance.ClearDebugHandlers();

    /// <summary>
    /// Function that can be set to return dynamic headers.
    /// 
    /// Headers specified in the constructor options will ALWAYS take precedence over headers returned by this function.
    /// </summary>
    public Func<Dictionary<string, string>>? GetHeaders { get; set; }

    /// <summary>
    /// Should be the first call to this class to initialize a connection with a Postgrest API Server
    /// </summary>
    /// <param name="baseUrl">Api Endpoint (ex: "http://localhost:8000"), no trailing slash required.</param>
    /// <param name="options">Optional client configuration.</param>
    /// <returns></returns>
    public Client(string baseUrl, ClientOptions? options = null)
    {
        this.BaseUrl = baseUrl;
        this.Options = options ?? new ClientOptions();
        this.httpClient = Helpers.ResolveHttpClient(this.Options);
    }


    /// <inheritdoc />
    public IPostgrestTable<T> Table<T>() where T : BaseModel, new() =>
        new Table<T>(this.BaseUrl, SerializerSettings(this.Options), this.Options)
        {
            GetHeaders = this.GetHeaders
        };

    /// <inheritdoc />
    public IPostgrestTableWithCache<T> Table<T>(IPostgrestCacheProvider cacheProvider)
        where T : BaseModel, new() =>
        new TableWithCache<T>(this.BaseUrl, cacheProvider, SerializerSettings(this.Options), this.Options)
        {
            GetHeaders = this.GetHeaders
        };

    /// <inheritdoc />
    public T Attach<T>(T model) where T : BaseModel
    {
        model.BaseUrl = this.BaseUrl;
        model.RequestClientOptions = this.Options;
        model.GetHeaders = this.GetHeaders;
        return model;
    }


    /// <inheritdoc />
    public async Task<TModeledResponse?> Rpc<TModeledResponse>(string procedureName, object? parameters = null)
    {
        var response = await this.Rpc(procedureName, parameters);

        return string.IsNullOrEmpty(response.Content) ? default : JsonSerializer.Deserialize<TModeledResponse>(response.Content!, SerializerSettings(this.Options));
    }

    /// <inheritdoc />
    public Task<BaseResponse> Rpc(string procedureName, object? parameters = null)
    {
        // Build Uri
        var builder = new UriBuilder($"{this.BaseUrl}/rpc/{procedureName}");

        var canonicalUri = builder.Uri.ToString();

        var serializerSettings = SerializerSettings(this.Options);

        // Prepare parameters
        Dictionary<string, object>? data = null;
        if (parameters != null)
            data = JsonSerializer.Deserialize<Dictionary<string, object>>(
                JsonSerializer.Serialize(parameters, serializerSettings), PostgrestSerializerOptions.Passthrough);

        // Prepare headers
        var headers = Helpers.PrepareRequestHeaders(HttpMethod.Post,
            new Dictionary<string, string>(this.Options.Headers), this.Options);

        if (this.GetHeaders != null)
            headers = this.GetHeaders().MergeLeft(headers);

        // Send request
        var request =
            Helpers.MakeRequestAsync(this.Options, this.httpClient, HttpMethod.Post, canonicalUri, serializerSettings, data, headers, operation: "rpc");
        return request;
    }
}
