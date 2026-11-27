using System.Reflection;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// The <c>name/version</c> that opens the <c>X-Client-Info</c> value: the client assembly's name in lowercase
/// kebab-case with a <c>-csharp</c> suffix, and its release version.
/// </summary>
internal sealed record ProductToken(string Name, string Version)
{
    private const string LanguageSuffix = "-csharp";
    private const int ReleaseVersionFieldCount = 3;

    internal static ProductToken Of(Assembly clientAssembly)
    {
        var assemblyName = clientAssembly.GetName();
        return new ProductToken(KebabCase(assemblyName.Name) + LanguageSuffix,
            ReleaseVersion.Of(clientAssembly) ?? AssemblyVersion(assemblyName));
    }

    public override string ToString() => $"{this.Name}/{this.Version}";

    private static string KebabCase(string assemblyName) => assemblyName.ToLowerInvariant().Replace('.', '-');

    private static string AssemblyVersion(AssemblyName assemblyName) =>
        (assemblyName.Version ?? new System.Version(0, 0, 0)).ToString(ReleaseVersionFieldCount);
}
