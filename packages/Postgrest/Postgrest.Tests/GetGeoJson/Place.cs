using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Postgrest.Tests.GetGeoJson;

[Table("places")]
public class Place : BaseModel
{
    [PrimaryKey("id")]
    public long Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("location")]
    public object? Location { get; set; }
}
