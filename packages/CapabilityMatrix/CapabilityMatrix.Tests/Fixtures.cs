using Supabase.CapabilityMatrix.CanonicalMatrix;

namespace Supabase.CapabilityMatrix.Tests;

/// <summary>Reads the sample source and spec that live as text under <c>Data/</c>.</summary>
internal static class Fixtures
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", name));

    public static FeatureCatalogue AuthCatalogue() => FeatureCatalogue.From([Read("sample-area.yaml")]);
}
