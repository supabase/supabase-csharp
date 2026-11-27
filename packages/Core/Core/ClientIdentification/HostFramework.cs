using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// The application framework hosting the SDK, detected from the loaded assemblies. MAUI wins over Blazor in
/// hybrid apps where both are present.
/// </summary>
internal sealed record HostFramework(string Name, string? Version)
{
    private const string UnityName = "unity";

    internal bool IsUnity => this.Name == UnityName;

    internal static HostFramework? Detect(HostEnvironment host)
    {
        var assemblies = ByName(host.LoadedAssemblies);
        if (assemblies.TryGetValue("Microsoft.Maui", out var maui))
            return new HostFramework("maui", ReleaseVersion.Of(maui));
        if (assemblies.ContainsKey("UnityEngine.CoreModule"))
            return new HostFramework(UnityName, ReleaseVersion.WithoutBuildMetadata(host.UnityVersion()));
        if (assemblies.TryGetValue("Microsoft.AspNetCore.Components", out var blazor))
            return new HostFramework("blazor", ReleaseVersion.Of(blazor));
        return null;
    }

    private static Dictionary<string, Assembly> ByName(IEnumerable<Assembly> assemblies) =>
        assemblies.GroupBy(assembly => assembly.GetName().Name).ToDictionary(group => group.Key, group => group.First());
}
