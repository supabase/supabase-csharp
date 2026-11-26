using Supabase.Storage.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// The identifier of an Iceberg table: the namespace that holds it and its name.
/// </summary>
public sealed record IcebergTable
{
    internal IcebergTable(IcebergNamespace @namespace, string name)
    {
        this.Namespace = @namespace;
        this.Name = name;
    }

    /// <summary>The namespace that holds the table.</summary>
    public IcebergNamespace Namespace { get; }

    /// <summary>The name of the table within its namespace.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => $"{this.Namespace}.{this.Name}";

    internal string Path => $"{this.CollectionPath}/{PathSegment.Escape(this.Name, "A table name", "table")}";

    internal string CollectionPath => this.Namespace.TablesPath;
}
