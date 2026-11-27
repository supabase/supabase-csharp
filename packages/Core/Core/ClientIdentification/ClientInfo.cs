using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// The SDK's own <c>X-Client-Info</c> value for a client assembly: its product token followed by the environment
/// parameters. The environment does not change during a process, so the value is built once per client assembly.
/// </summary>
internal sealed record ClientInfo(ProductToken Product, IReadOnlyList<ClientInfoParameter> Parameters)
{
    internal const string ParameterSeparator = "; ";

    private static readonly ConcurrentDictionary<Assembly, ClientInfo> PerClientAssembly = new();

    internal string HeaderValue { get; } = string.Join(ParameterSeparator, Parameters.Select(parameter => parameter.ToString()).Prepend(Product.ToString()));

    internal static ClientInfo Of(Type clientType) =>
        PerClientAssembly.GetOrAdd(clientType.Assembly, static clientAssembly => Of(clientAssembly, HostEnvironment.Current()));

    internal static ClientInfo Of(Assembly clientAssembly, HostEnvironment host) =>
        new(ProductToken.Of(clientAssembly), ClientEnvironment.Detect(host).Parameters());
}
