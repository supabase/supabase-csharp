#region

using System;
using System.IO;

#endregion

namespace Gotrue.Tests.UpdateFactor;

/// <summary>
///     Factor bodies as the auth server returns them, read from <c>UpdateFactor/Data</c>.
/// </summary>
internal static class FactorResponses
{
    internal static string PhoneFactor => File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateFactor", "Data", "phone_factor.json"));
}
