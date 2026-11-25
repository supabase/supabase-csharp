using System.ComponentModel;

namespace Supabase.Storage.ListAnalyticsBuckets;

/// <summary>
/// The order analytics buckets are listed in.
/// </summary>
/// <param name="Column">The column to sort by.</param>
/// <param name="Direction">The sort direction. Ascending unless specified.</param>
public sealed record AnalyticsBucketSort(AnalyticsBucketSortColumn Column, ListSortDirection Direction = ListSortDirection.Ascending);
