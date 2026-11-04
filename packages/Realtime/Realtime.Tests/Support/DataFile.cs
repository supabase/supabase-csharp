using System;
using System.IO;

namespace Realtime.Tests.Support;

/// <summary>Reads a fixture from a use case's <c>Data/</c> folder, copied next to the test assembly.</summary>
internal static class DataFile
{
    public static string Read(string useCase, string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, useCase, "Data", name)).Trim();
}
