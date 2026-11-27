using System.Reflection;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// The release version an assembly reports, without the build metadata (<c>+&lt;commit&gt;</c>) that
/// Source Link appends to the informational version.
/// </summary>
internal static class ReleaseVersion
{
    private const char BuildMetadataSeparator = '+';

    internal static string? Of(Assembly assembly) =>
        WithoutBuildMetadata(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    internal static string? WithoutBuildMetadata(string? version) => version?.Split(BuildMetadataSeparator)[0];
}
