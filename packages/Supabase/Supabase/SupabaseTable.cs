using System.Collections.Generic;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Interfaces;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace Supabase;

/// <summary>
/// A Supabase wrapper for a Postgrest Table.
/// </summary>
/// <typeparam name="TModel">Model that implements <see cref="BaseModel"/></typeparam>
public class SupabaseTable<TModel> : Table<TModel>, ISupabaseTable<TModel, RealtimeChannel>
    where TModel : BaseModel, new()
{
    private RealtimeChannel? channel;
    private readonly IPostgrestClient postgrestClient;
    private readonly IRealtimeClient<RealtimeSocket, RealtimeChannel> realtimeClient;
    private readonly string schema;

    /// <summary>
    /// A Supabase wrapper for a Postgrest table.
    /// </summary>
    /// <param name="postgrestClient"></param>
    /// <param name="realtimeClient"></param>
    /// <param name="schema"></param>
    public SupabaseTable(IPostgrestClient postgrestClient,
        IRealtimeClient<RealtimeSocket, RealtimeChannel> realtimeClient, string schema = "public") : base(
        postgrestClient.BaseUrl, Postgrest.Client.SerializerSettings(postgrestClient.Options),
        postgrestClient.Options)
    {
        this.postgrestClient = postgrestClient;
        this.realtimeClient = realtimeClient;
        this.schema = schema;
        this.GetHeaders = postgrestClient.GetHeaders;
    }

    /// <inheritdoc />
    [ImplementedCapability("realtime.subscriptions.postgres_changes", "Model-typed path via SupabaseTable.On (blessed) plus the low-level Channel().Register(PostgresChangesOptions).")]
    public async Task<RealtimeChannel> On(ListenType listenType, IRealtimeChannel.PostgresChangesHandler handler)
    {
        if (this.channel == null)
        {
            var parameters = new Dictionary<string, string>();

            // In regard to: https://github.com/supabase/supabase-js/pull/270
            var headers = this.postgrestClient?.GetHeaders?.Invoke();
            if (headers != null && headers.TryGetValue("Authorization", out var header))
            {
                parameters.Add("user_token", header.Split(' ')[1]);
            }

            this.channel = this.realtimeClient.Channel("realtime", this.schema, this.TableName, parameters: parameters);
        }

        if (this.realtimeClient.Socket == null || !this.realtimeClient.Socket.IsConnected)
            await this.realtimeClient.ConnectAsync();

        this.channel.AddPostgresChangeHandler(listenType, handler);

        await this.channel.Subscribe();
        return this.channel;
    }
}
