using Supabase.Core.Attributes;
#pragma warning disable CS1591
namespace Supabase.Postgrest;


public static class Constants
{
    /// <summary>
    /// See: https://postgrest.org/en/v7.0.0/api.html?highlight=operators#operators
    /// </summary>
    public enum Operator
    {
        [MapTo("and")]
        And,
        [MapTo("or")]
        [ImplementedCapability("database.using_filters.or")]
        Or,
        [MapTo("eq")]
        [ImplementedCapability("database.using_filters.eq")]
        Equals,
        [MapTo("gt")]
        [ImplementedCapability("database.using_filters.gt")]
        GreaterThan,
        [MapTo("gte")]
        [ImplementedCapability("database.using_filters.gte")]
        GreaterThanOrEqual,
        [MapTo("lt")]
        [ImplementedCapability("database.using_filters.lt")]
        LessThan,
        [MapTo("lte")]
        [ImplementedCapability("database.using_filters.lte")]
        LessThanOrEqual,
        [MapTo("neq")]
        [ImplementedCapability("database.using_filters.neq")]
        NotEqual,
        [MapTo("like")]
        [ImplementedCapability("database.using_filters.like")]
        Like,
        [MapTo("ilike")]
        [ImplementedCapability("database.using_filters.ilike")]
        ILike,
        [MapTo("in")]
        [ImplementedCapability("database.using_filters.in")]
        In,
        [MapTo("is")]
        [ImplementedCapability("database.using_filters.is")]
        Is,
        [MapTo("fts")]
        [ImplementedCapability("database.using_filters.text_search", "FTS family: fts/plfts/phfts/wfts.")]
        FTS,
        [MapTo("plfts")]
        [ImplementedCapability("database.using_filters.text_search", "FTS family: fts/plfts/phfts/wfts.")]
        PLFTS,
        [MapTo("phfts")]
        [ImplementedCapability("database.using_filters.text_search", "FTS family: fts/plfts/phfts/wfts.")]
        PHFTS,
        [MapTo("wfts")]
        [ImplementedCapability("database.using_filters.text_search", "FTS family: fts/plfts/phfts/wfts.")]
        WFTS,
        [MapTo("cs")]
        [ImplementedCapability("database.using_filters.contains")]
        Contains,
        [MapTo("cd")]
        [ImplementedCapability("database.using_filters.contained_by")]
        ContainedIn,
        [MapTo("ov")]
        [ImplementedCapability("database.using_filters.overlaps")]
        Overlap,
        [MapTo("sl")]
        [ImplementedCapability("database.using_filters.range_lt")]
        StrictlyLeft,
        [MapTo("sr")]
        [ImplementedCapability("database.using_filters.range_gt")]
        StrictlyRight,
        [MapTo("nxr")]
        [ImplementedCapability("database.using_filters.range_lte")]
        NotRightOf,
        [MapTo("nxl")]
        [ImplementedCapability("database.using_filters.range_gte")]
        NotLeftOf,
        [MapTo("adj")]
        [ImplementedCapability("database.using_filters.range_adjacent")]
        Adjacent,
        [MapTo("not")]
        [ImplementedCapability("database.using_filters.not")]
        Not,
    }

    public enum Ordering
    {
        [MapTo("asc")]
        Ascending,
        [MapTo("desc")]
        Descending,
    }

    /// <summary>
    /// See: https://postgrest.org/en/v7.0.0/api.html?highlight=nulls%20first#ordering
    /// </summary>
    public enum NullPosition
    {
        [MapTo("nullsfirst")]
        First,
        [MapTo("nullslast")]
        Last
    }

    /// <summary>
    /// See: https://postgrest.org/en/v7.0.0/api.html?highlight=count#estimated-count
    /// </summary>
    public enum CountType
    {
        [MapTo("exact")]
        Exact,
        [MapTo("planned")]
        Planned,
        [MapTo("estimated")]
        Estimated
    }
}
