using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Postgrest.Tests.BulkWrite;

[Table("users")]
public class SparseUser : BaseModel
{
    [PrimaryKey("username", true)]
    public string? Username { get; set; }

    [Column("data", NullValueHandling = JsonIgnoreCondition.WhenWritingNull)]
    public string? Data { get; set; }

    [Column("catchphrase", NullValueHandling = JsonIgnoreCondition.WhenWritingNull)]
    public string? Catchphrase { get; set; }
}
