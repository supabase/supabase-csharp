using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Support;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.GetGeoJson;

/// <summary>
///     Against the live stack, with PostGIS installed, <c>GetGeoJsonAsync</c> returns each matching row as a
///     feature whose geometry is the row's PostGIS geometry and whose properties are its other columns.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class GetGeoJsonTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetGeoJsonAsync_ShouldReturnTheMatchingRowsAsFeatures()
    {
        var client = LocalStack.Client();
        var name = $"geojson-{Guid.NewGuid():N}";
        await client.Table<Place>().Insert(new Place { Name = name, Location = "SRID=4326;POINT(-71.10044 42.373695)" },
            new QueryOptions { Returning = QueryOptions.ReturnType.Minimal }, this.TestContext.CancellationToken);
        try
        {
            var feature = (await client.Table<Place>().Select("name,location").Filter("name", Operator.Equals, name)
                .GetGeoJsonAsync(this.TestContext.CancellationToken)).Features.Single();
            (feature.Properties.Name, feature.Geometry?.GetProperty("type").GetString())
                .Should().Be((name, "Point"), "the geometry column becomes the feature's geometry and the other columns its properties");
        }
        finally
        {
            await client.Table<Place>().Filter("name", Operator.Equals, name).Delete();
        }
    }
}
