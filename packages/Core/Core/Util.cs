using System;
using Supabase.Core.ClientIdentification;

namespace Supabase.Core;

/// <summary>
/// Shared utilities for Supabase client libraries.
/// </summary>
public static class Util
{
    /// <summary>
    /// Builds the <c>X-Client-Info</c> header value for the given client type.
    /// </summary>
    /// <remarks>
    /// Format: <c>name-csharp/version[; key=value ...]</c>, where <c>name</c> is the client assembly's name in
    /// lowercase kebab-case and <c>version</c> its release version. The parameters describe the platform, its
    /// version and processor architecture, the .NET runtime and its version, and the application framework when
    /// one is detected. A parameter the SDK cannot determine is left out.
    /// </remarks>
    /// <param name="clientType">A type belonging to the client assembly, used to resolve the assembly name and version.</param>
    /// <returns>A structured header value identifying the client library and its host environment.</returns>
    public static string GetAssemblyVersion(Type clientType) => ClientInfo.Of(clientType).HeaderValue;
}
