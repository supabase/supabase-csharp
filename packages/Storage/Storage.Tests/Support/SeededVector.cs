using System.Collections.Generic;

namespace Storage.Tests.Support;

/// <summary>A vector written to the index over plain HTTP before a test runs. Its components must be exact in half precision, which the local pgvector store uses.</summary>
public sealed record SeededVector(string Key, IReadOnlyList<float> Data, IReadOnlyDictionary<string, object>? Metadata = null);
