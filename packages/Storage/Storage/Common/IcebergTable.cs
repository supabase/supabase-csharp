using System;

namespace Supabase.Storage.Common;

/// <summary>
/// The identifier of an Iceberg table: the namespace that holds it and its name.
/// </summary>
public sealed record IcebergTable
{
    private IcebergTable(IcebergNamespace @namespace, string name)
    {
        this.Namespace = @namespace;
        this.Name = name;
    }

    /// <summary>The namespace that holds the table.</summary>
    public IcebergNamespace Namespace { get; }

    /// <summary>The name of the table within its namespace.</summary>
    public string Name { get; }

    /// <summary>
    /// Builds the identifier of a table. The service validates the name.
    /// </summary>
    /// <param name="namespace">The namespace that holds the table.</param>
    /// <param name="name">The name of the table within its namespace.</param>
    /// <returns>The identifier of the table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="namespace"/> or <paramref name="name"/> is null.</exception>
    /// <example>
    /// <code>
    /// var events = IcebergTable.Of(IcebergNamespace.Of("sales"), "events");
    /// </code>
    /// </example>
    public static IcebergTable Of(IcebergNamespace @namespace, string name) =>
        new(@namespace ?? throw new ArgumentNullException(nameof(@namespace)), name ?? throw new ArgumentNullException(nameof(name)));

    /// <inheritdoc />
    public override string ToString() => $"{this.Namespace}.{this.Name}";
}
