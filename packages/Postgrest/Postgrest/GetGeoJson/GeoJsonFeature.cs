using System.Text.Json;
using Supabase.Postgrest.Models;

namespace Supabase.Postgrest.GetGeoJson;

/// <summary>
/// One row as a GeoJSON <c>Feature</c>: the row's geometry, and its other columns as the model.
/// </summary>
/// <typeparam name="TModel">The model of the table that was read.</typeparam>
public sealed record GeoJsonFeature<TModel> where TModel : BaseModel, new()
{
    /// <summary>
    /// The GeoJSON geometry object of the row's first geometry column, as PostGIS produced it, or null when the
    /// row has no geometry.
    /// </summary>
    public JsonElement? Geometry { get; init; }

    /// <summary>
    /// The row's other selected columns. The geometry column is not among them.
    /// </summary>
    public TModel Properties { get; init; } = new();
}
