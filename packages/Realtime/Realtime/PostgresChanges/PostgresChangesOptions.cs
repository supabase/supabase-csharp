using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Core.Attributes;

namespace Supabase.Realtime.PostgresChanges;

/// <summary>
/// Handles a `postgres_changes` channel
/// 
/// For Example in the js client: 
/// 
///		const databaseFilter = {
///			schema: 'public',
///			table: 'messages',
///			filter: `room_id=eq.${channelId}`,
///			event: 'INSERT',
///		}
///	
/// Would translate to:
/// 
///		new PostgresChangesOptions("public", "messages", $"room_id=eq.{channelId}");
/// </summary>
public class PostgresChangesOptions
{
    /// <summary>
    /// Mapping of postgres changes listener types
    /// </summary>
    public enum ListenType
    {
        /// <summary>
        /// All event
        /// </summary>
        [MapTo("*")]
        All,
        /// <summary>
        /// INSERT events
        /// </summary>
        [MapTo("INSERT")]
        Inserts,
        /// <summary>
        /// UPDATE events
        /// </summary>
        [MapTo("UPDATE")]
        Updates,
        /// <summary>
        /// DELETE events
        /// </summary>
        [MapTo("DELETE")]
        Deletes
    }

    /// <summary>
    /// The schema for this listener, likely: `public`
    /// </summary>
    [JsonPropertyName("schema")]
    public string Schema { get; set; }

    /// <summary>
    /// The table for this listener, can be: `*` matching all tables in schema. When <c>null</c>
    /// (a schema-wide listener), the <c>table</c> key is omitted from the join payload rather than
    /// sent as <c>null</c>.
    /// </summary>
    [JsonPropertyName("table")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Table { get; set; }

    /// <summary>
    /// The filter for this listener
    /// </summary>
    [JsonPropertyName("filter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [ImplementedCapability("realtime.subscriptions.postgres_changes_filter", "Filter is a hand-built PostgREST string (e.g. 'user_id=eq.{id}'), untied to the model.")]
    public string? Filter { get; set; }

    /// <summary>
    /// The parameters passed to the server
    /// </summary>
    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Parameters { get; set; }

    /// <summary>
    /// The stringified event listener type
    /// </summary>
    [JsonPropertyName("event")]
    public string Event => Core.Helpers.GetMappedToAttr(this.listenType).Mapping!;

    private readonly ListenType listenType;

    /// <summary>
    /// Postgres changes options.
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="table"></param>
    /// <param name="eventType"></param>
    /// <param name="filter"></param>
    /// <param name="parameters"></param>
    public PostgresChangesOptions(string schema, string? table = null, ListenType eventType = ListenType.All, string? filter = null, Dictionary<string, string>? parameters = null)
    {
        this.listenType = eventType;
        this.Schema = schema;
        this.Table = table;
        this.Filter = filter;
        this.Parameters = parameters;
    }

    private bool Equals(PostgresChangesOptions other) => this.listenType == other.listenType && this.Schema == other.Schema && this.Table == other.Table && this.Filter == other.Filter;

    /// <summary>
    /// Check if object are equals 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (obj.GetType() != this.GetType()) return false;
        return this.Equals((PostgresChangesOptions) obj);
    }

    /// <summary>
    /// Generate hash code
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int) this.listenType;
            hashCode = (hashCode * 397) ^ this.Schema.GetHashCode();
            hashCode = (hashCode * 397) ^ (this.Table != null ? this.Table.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (this.Filter != null ? this.Filter.GetHashCode() : 0);
            return hashCode;
        }
    }
}
