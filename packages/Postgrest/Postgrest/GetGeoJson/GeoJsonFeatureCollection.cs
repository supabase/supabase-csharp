using System;
using System.Collections.Generic;
using Supabase.Postgrest.Models;

namespace Supabase.Postgrest.GetGeoJson;

/// <summary>
/// The rows of a read as a GeoJSON <c>FeatureCollection</c>: one feature per row.
/// </summary>
/// <typeparam name="TModel">The model of the table that was read.</typeparam>
public sealed record GeoJsonFeatureCollection<TModel> where TModel : BaseModel, new()
{
    /// <summary>
    /// One feature per row, in the order PostgREST returned them.
    /// </summary>
    public IReadOnlyList<GeoJsonFeature<TModel>> Features { get; init; } = Array.Empty<GeoJsonFeature<TModel>>();
}
