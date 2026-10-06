using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Realtime.Tests.Models;

/// <summary>
///     A row whose column names collide with <c>SocketResponsePayload</c>'s typed members (supabase-csharp#486):
///     <c>type</c> is numeric here but a string on the payload, so projecting the record onto that payload throws
///     under System.Text.Json unless the record slot is read untyped.
/// </summary>
[Table("things")]
public class Thing : BaseModel
{
    [PrimaryKey("id", false)] public long Id { get; set; }

    [Column("type")] public int Type { get; set; }

    [Column("name")] public string? Name { get; set; }
}
