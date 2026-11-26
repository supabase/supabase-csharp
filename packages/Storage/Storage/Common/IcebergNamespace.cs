using System;
using System.Collections.Generic;
using System.Linq;

namespace Supabase.Storage.Common;

/// <summary>
/// The identifier of an Iceberg namespace: one or more levels, outermost first.
/// </summary>
public sealed record IcebergNamespace
{
    private const char LevelSeparator = '\u001F';

    private IcebergNamespace(IReadOnlyList<string> levels) => this.Levels = levels;

    /// <summary>The levels of the namespace, outermost first. Never empty.</summary>
    public IReadOnlyList<string> Levels { get; }

    /// <summary>
    /// Builds the identifier of a namespace. The service validates the names of the levels.
    /// </summary>
    /// <param name="levels">The levels of the namespace, outermost first.</param>
    /// <returns>The identifier of the namespace.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="levels"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// There is no level, or a level is null, empty, whitespace or contains the unit separator (U+001F)
    /// the Iceberg REST Catalog API uses to join levels.
    /// </exception>
    /// <example>
    /// <code>
    /// var sales = IcebergNamespace.Of("sales");
    /// var europe = IcebergNamespace.Of("sales", "europe");
    /// </code>
    /// </example>
    public static IcebergNamespace Of(params string[] levels)
    {
        RejectMissing(levels);
        levels.ToList().ForEach(RejectUnusable);
        return new IcebergNamespace(Array.AsReadOnly(levels.ToArray()));
    }

    /// <summary>
    /// Builds the identifier of a table held by this namespace. The service validates the name.
    /// </summary>
    /// <param name="name">The name of the table within this namespace.</param>
    /// <returns>The identifier of the table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <example>
    /// <code>
    /// var events = IcebergNamespace.Of("sales").Table("events");
    /// </code>
    /// </example>
    public IcebergTable Table(string name) =>
        new(this, name ?? throw new ArgumentNullException(nameof(name)));

    /// <inheritdoc />
    public bool Equals(IcebergNamespace? other) =>
        other is not null && this.Levels.SequenceEqual(other.Levels);

    /// <inheritdoc />
    public override int GetHashCode() =>
        this.Levels.Aggregate(0, (hash, level) => HashCode.Combine(hash, level));

    /// <inheritdoc />
    public override string ToString() => string.Join(".", this.Levels);

    internal string Joined() => string.Join(LevelSeparator.ToString(), this.Levels);

    internal string Path => $"namespaces/{PathSegment.Escape(this.Joined(), "A namespace", "namespace")}";

    private static void RejectMissing(string[]? levels)
    {
        if (levels is null)
            throw new ArgumentNullException(nameof(levels));

        if (levels.Length == 0)
            throw new ArgumentException("A namespace needs at least one level.", nameof(levels));
    }

    private static void RejectUnusable(string level)
    {
        if (string.IsNullOrWhiteSpace(level))
            throw new ArgumentException("A namespace level must be provided.", "levels");

        if (level.Contains(LevelSeparator))
            throw new ArgumentException("A namespace level cannot contain the unit separator (U+001F).", "levels");
    }
}
