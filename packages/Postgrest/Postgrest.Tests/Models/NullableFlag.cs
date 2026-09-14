#nullable enable
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Postgrest.Tests.Models;

/// <summary>
/// A model with a nullable boolean column, used to assert that a bare `Nullable&lt;bool&gt;.Value`
/// predicate (i.e. `x => x.IsActive!.Value`) resolves to the mapped column rather than `Value`.
/// URL-generation only; it is not backed by a seeded table.
/// </summary>
[Table("nullable_flag")]
public class NullableFlag : BaseModel
{
    [PrimaryKey("id")] public int? Id { get; set; }

    [Column("is_active")] public bool? IsActive { get; set; }
}
