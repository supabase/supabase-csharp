using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Interfaces;
using Supabase.Postgrest.Explain;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;

namespace Supabase.Postgrest.Interfaces;

/// <summary>
/// Client interface for Postgrest
/// </summary>
/// <typeparam name="TModel"></typeparam>
public interface IPostgrestTable<TModel> : IGettableHeaders where TModel : BaseModel, new()
{
    /// <summary>
    /// API Base Url for subsequent calls.
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// Name of the Table parsed by the Model.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Generates the encoded URL with defined query parameters that will be sent to the Postgrest API.
    /// </summary>
    string GenerateUrl();

    /// <summary>
    /// Adds an AND Filter to the current query args.
    /// </summary>
    /// <param name="filters"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> And(List<IPostgrestQueryFilter> filters);

    /// <summary>
    /// Clears currently defined query values.
    /// </summary>
    void Clear();

    /// <summary>
    /// By using the columns query parameter it’s possible to specify the payload keys that will be inserted and ignore the rest of the payload.
    /// 
    /// The rest of the JSON keys will be ignored.
    /// Using this also has the side-effect of being more efficient for Bulk Insert since PostgREST will not process the JSON and it’ll send it directly to PostgreSQL.
    /// 
    /// See: https://postgrest.org/en/stable/api.html#specifying-columns
    /// </summary>
    /// <param name="columns"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Columns(string[] columns);

    /// <summary>
    /// By using the columns query parameter it’s possible to specify the payload keys that will be inserted and ignore the rest of the payload.
    /// 
    /// The rest of the JSON keys will be ignored.
    /// Using this also has the side-effect of being more efficient for Bulk Insert since PostgREST will not process the JSON and it’ll send it directly to PostgreSQL.
    /// 
    /// See: https://postgrest.org/en/stable/api.html#specifying-columns
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Columns(Expression<Func<TModel, object[]>> predicate);

    /// <summary>
    /// Returns ONLY a count from the specified query.
    ///
    /// See: https://postgrest.org/en/v7.0.0/api.html?highlight=count
    /// </summary>
    /// <param name="type">The kind of count.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> Count(Constants.CountType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a delete request using the defined query params on the current instance.
    ///
    /// The deleted rows are returned in <see cref="ModeledResponse{T}.Models"/> (subject to
    /// <see cref="QueryOptions.Returning"/>). A row-level-security policy that hides a row, or a filter that
    /// matches nothing, is not an error in PostgREST: the request succeeds and returns an empty representation.
    /// Inspect <see cref="ModeledResponse{T}.Models"/> to tell "nothing was deleted" apart from "deleted" —
    /// the SDK does not decide that for you.
    /// </summary>
    /// <returns>The response, whose <see cref="ModeledResponse{T}.Models"/> holds the rows that were deleted.</returns>
    /// <example>
    /// <code>
    /// var response = await client.Table&lt;Todo&gt;().Filter("id", Operator.Equals, "1").Delete();
    /// if (response.Models.Count == 0)
    /// {
    ///     // Nothing was deleted: the row did not exist, or an RLS policy hid it. Decide how to react.
    /// }
    /// </code>
    /// </example>
    Task<ModeledResponse<TModel>> Delete(QueryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a delete request using the model's primary key as the filter for the request.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ModeledResponse<TModel>> Delete(TModel model, QueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a Filter to a query request
    /// </summary>
    /// <param name="columnName">Column Name in Table.</param>
    /// <param name="op">Operation to perform.</param>
    /// <param name="criterion">Value to filter with, must be a `string`, `List&lt;object&gt;`, `Dictionary&lt;string, object&gt;`, `FullTextSearchConfig`, or `Range`</param>
    /// <returns></returns>
    IPostgrestTable<TModel> Filter<TCriterion>(string columnName, Constants.Operator op, TCriterion? criterion);

    /// <summary>
    /// Add a filter to a query request using a predicate to select column.
    /// </summary>
    /// <param name="predicate">Expects a columns from the Model to be returned</param>
    /// <param name="op">Operation to perform.</param>
    /// <param name="criterion">Value to filter with, must be a `string`, `List&lt;object&gt;`, `Dictionary&lt;string, object&gt;`, `FullTextSearchConfig`, or `Range`</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    IPostgrestTable<TModel> Filter<TCriterion>(Expression<Func<TModel, object>> predicate, Constants.Operator op,
        TCriterion? criterion);

    /// <summary>
    /// Executes the query using the defined filters on the current instance.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="countType"></param>
    /// <returns></returns>
    Task<ModeledResponse<TModel>> Get(CancellationToken cancellationToken = default, Constants.CountType countType = Constants.CountType.Estimated);

    /// <summary>
    /// Executes a BULK INSERT query using the defined query params on the current instance.
    /// Rows may carry different fields (for example when a column omits its null value): the request names the
    /// union of the fields in the `columns` query parameter, unless <see cref="Columns(string[])"/> was called.
    /// An empty collection writes nothing.
    /// </summary>
    /// <example>
    /// <code>
    /// await client.Table&lt;User&gt;().Insert(new[] { new User { Username = "ann" }, new User { Username = "bob", Data = "{}" } });
    /// </code>
    /// </example>
    /// <param name="models"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>A typed model response from the database.</returns>
    Task<ModeledResponse<TModel>> Insert(ICollection<TModel> models, QueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an INSERT query using the defined query params on the current instance.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>A typed model response from the database.</returns>
    Task<ModeledResponse<TModel>> Insert(TModel model, QueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a limit with an optional foreign table reference. 
    /// </summary>
    /// <param name="limit"></param>
    /// <param name="foreignTableName"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Limit(int limit, string? foreignTableName = null);

    /// <summary>
    /// Finds all rows whose columns match the specified `query` object.
    /// </summary>
    /// <param name="query">The object to filter with, with column names as keys mapped to their filter values.</param>
    /// <returns></returns>
    IPostgrestTable<TModel> Match(Dictionary<string, string> query);

    /// <summary>
    /// Fills in query parameters based on a given model's primary key(s).
    /// </summary>
    /// <param name="model">A model with a primary key column</param>
    /// <returns></returns>
    IPostgrestTable<TModel> Match(TModel model);

    /// <summary>
    /// Adds a NOT filter to the current query args.
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Not(IPostgrestQueryFilter filter);

    /// <summary>
    /// Adds a NOT filter to the current query args.
    /// </summary>
    /// <param name="columnName"></param>
    /// <param name="op"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Not(string columnName, Constants.Operator op, Dictionary<string, object> criteria);

    /// <summary>
    /// Adds a NOT filter to the current query args.
    /// </summary>
    /// <param name="predicate">Expects a column from the model to be returned.</param>
    /// <param name="op"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Not(Expression<Func<TModel, object>> predicate, Constants.Operator op, Dictionary<string, object> criteria);

    /// <summary>
    /// Adds a NOT filter to the current query args.
    /// Allows queries like:
    /// <code>
    /// await client.Table&lt;User&gt;().Not("status", Operators.In, new List&lt;string&gt; {"AWAY", "OFFLINE"}).Get();
    /// </code>
    /// </summary>
    /// <param name="columnName"></param>
    /// <param name="op"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Not<TCriterion>(string columnName, Constants.Operator op, List<TCriterion> criteria);

    /// <summary>
    /// Adds a NOT filter to the current query args.
    /// Allows queries like:
    /// <code>
    /// await client.Table&lt;User&gt;().Not("status", Operators.In, new List&lt;string&gt; {"AWAY", "OFFLINE"}).Get();
    /// </code>
    /// </summary>
    /// <param name="predicate">Expects a column from the model to be returned.</param>
    /// <param name="op"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Not<TCriterion>(Expression<Func<TModel, object>> predicate, Constants.Operator op,
        List<TCriterion> criteria);

    /// <summary>
    /// Adds a NOT filter to the current query args.
    ///
    /// Allows queries like:
    /// <code>
    /// await client.Table&lt;User&gt;().Not("status", Operators.Equal, "OFFLINE").Get();
    /// </code>
    /// </summary>
    /// <param name="columnName"></param>
    /// <param name="op"></param>
    /// <param name="criterion"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Not<TCriterion>(string columnName, Constants.Operator op, TCriterion? criterion);


    /// <summary>
    /// Adds a NOT filter to the current query args.
    /// 
    /// Allows queries like:
    /// <code>
    /// await client.Table&lt;User&gt;().Not("status", Operators.Equal, "OFFLINE").Get();
    /// </code>
    /// </summary>
    /// <param name="predicate">Expects a column from the model to be returned.</param>
    /// <param name="op"></param>
    /// <param name="criterion"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Not<TCriterion>(Expression<Func<TModel, object>> predicate, Constants.Operator op,
        TCriterion? criterion);

    /// <summary>
    /// Sets an offset with an optional foreign table reference.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="foreignTableName"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Offset(int offset, string? foreignTableName = null);

    /// <summary>
    /// By specifying the onConflict query parameter, you can make UPSERT work on a column(s) that has a UNIQUE constraint.
    /// </summary>
    /// <param name="columnName"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> OnConflict(string columnName);

    /// <summary>
    /// Set an onConflict query parameter for UPSERTing on a column that has a UNIQUE constraint using a linq predicate.
    /// </summary>
    /// <param name="predicate">Expects a column from the model to be returned.</param>
    /// <returns></returns>
    IPostgrestTable<TModel> OnConflict(Expression<Func<TModel, object>> predicate);

    /// <summary>
    /// Adds a OR Filter to the current query args.
    /// </summary>
    /// <param name="filters"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Or(List<IPostgrestQueryFilter> filters);

    /// <summary>
    /// Adds an ordering to the current query args.
    /// 
    /// NOTE: If multiple orderings are required, chain this function with another call to <see>
    ///     <cref>Order(Expression{Func{T,object}},Ordering,NullPosition)</cref>
    /// </see>
    /// .
    /// </summary>
    /// <param name="column">Column Name</param>
    /// <param name="ordering"></param>
    /// <param name="nullPosition"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Order(string column, Constants.Ordering ordering,
        Constants.NullPosition nullPosition = Constants.NullPosition.First);

    /// <summary>
    /// Adds an ordering to the current query args using a predicate function.
    /// 
    /// NOTE: If multiple orderings are required, chain this function with another call to <see>
    ///     <cref>Order(Expression{Func{T,object}},Ordering,NullPosition)</cref>
    /// </see>
    /// .
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="ordering">>Expects a columns from the Model to be returned</param>
    /// <param name="nullPosition"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Order(Expression<Func<TModel, object>> predicate, Constants.Ordering ordering,
        Constants.NullPosition nullPosition = Constants.NullPosition.First);

    /// <summary>
    /// Adds an ordering to the current query args.
    /// 
    /// NOTE: If multiple orderings are required, chain this function with another call to <see>
    ///     <cref>Order(Expression{Func{T,object}},Ordering,NullPosition)</cref>
    /// </see>
    /// .
    /// </summary>
    /// <param name="foreignTable"></param>
    /// <param name="column"></param>
    /// <param name="ordering"></param>
    /// <param name="nullPosition"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Order(string foreignTable, string column, Constants.Ordering ordering,
        Constants.NullPosition nullPosition = Constants.NullPosition.First);

    /// <summary>
    /// Sets a FROM range, similar to a `StartAt` query.
    /// </summary>
    /// <param name="from"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Range(int from);

    /// <summary>
    /// Sets a bounded range to the current query.
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Range(int from, int to);

    /// <summary>
    /// Select columns for query. 
    /// </summary>
    /// <param name="columnQuery"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Select(string columnQuery);

    /// <summary>
    /// Select columns using a predicate function.
    /// 
    /// For example: 
    ///		`Table&lt;Movie&gt;().Select(x => new[] { x.Id, x.Name, x.CreatedAt }).Get();`
    /// </summary>
    /// <param name="predicate">Expects an array of columns from the Model to be returned.</param>
    /// <returns></returns>
    IPostgrestTable<TModel> Select(Expression<Func<TModel, object[]>> predicate);

    /// <summary>
    /// Filter a query based on a predicate expression. The expression is translated into a
    /// Postgrest filter and evaluated server-side - it is never executed as C# code.
    ///
    /// Note: Chaining multiple <see cref="Where(Expression{Func{TModel, bool}})"/> calls will
    /// be parsed as an "AND" query.
    ///
    /// Examples:
    ///		`Table&lt;Movie&gt;().Where(x =&gt; x.Name == "Top Gun").Get();`
    ///		`Table&lt;Movie&gt;().Where(x =&gt; x.Name == "Top Gun" || x.Name == "Mad Max").Get();`
    ///		`Table&lt;Movie&gt;().Where(x =&gt; x.Name.Contains("Gun")).Get();`
    ///		`Table&lt;Movie&gt;().Where(x =&gt; x.CreatedAt &lt;= new DateTime(2022, 08, 21)).Get();`
    ///		`Table&lt;Movie&gt;().Where(x =&gt; x.Id > 5 &amp;&amp; x.Name.Contains("Max")).Get();`
    /// </summary>
    /// <remarks>
    /// Supported predicate shapes:
    /// <list type="bullet">
    /// <item><description>The left side of a comparison must be a Model property (with a `Column` or `PrimaryKey` attribute); the right side a constant, captured variable, or instantiation (`new DateTime(...)`).</description></item>
    /// <item><description>Comparisons (`==`, `!=`, `&gt;`, `&gt;=`, `&lt;`, `&lt;=`) can be combined with `&amp;&amp;` and `||`, and `String`/collection `Contains` is supported.</description></item>
    /// <item><description>`Contains` translates by which side is the Model: a column containing a constant (`x =&gt; x.Tags.Contains("a")`) becomes a `cs`/`like` filter, while a captured collection containing a column (`x =&gt; ids.Contains(x.Id)`) becomes an `in` filter.</description></item>
    /// <item><description>A boolean column can be used as a predicate on its own: `x =&gt; x.IsActive` becomes an `eq` filter and `x =&gt; !x.IsActive` a `not.eq` filter.</description></item>
    /// <item><description>Negation (`!`) is translated to a Postgrest `not.` filter: `x =&gt; !(x.Name == "Top Gun")` becomes `not.eq`, and a negated group `x =&gt; !(x.Name == "Top Gun" || x.Id &gt; 5)` becomes `not.or=(...)`.</description></item>
    /// <item><description>Null checks translate to Postgrest `is null` filters: `x =&gt; x.Name == null` and `x =&gt; x.Name == null || x.Name == "Top Gun"` both work.</description></item>
    /// <item><description>Conditions that never reference the Model (i.e. `x =&gt; localVariable == null`) are evaluated locally: an always-true predicate applies no filter, an always-false one throws.</description></item>
    /// </list>
    ///
    /// Comparing two Model columns to each other (`x =&gt; x.StartDate &lt; x.EndDate`) is not supported:
    /// Postgrest has no column-to-column filter syntax, so it throws an <see cref="ArgumentException"/> —
    /// use a database computed/generated column or an RPC instead.
    ///
    /// Invoking a delegate (`Func&lt;TModel, bool&gt;`) inside the predicate is not supported: a compiled
    /// delegate cannot be translated into a Postgrest filter and throws an <see cref="ArgumentException"/>.
    /// To apply an optional filter, declare it as an `Expression&lt;Func&lt;TModel, bool&gt;&gt;` and pass it
    /// conditionally:
    /// <code>
    ///	var query = client.Table&lt;Movie&gt;();
    ///	if (optionalFilter != null)
    ///		query = query.Where(optionalFilter);
    /// </code>
    /// </remarks>
    /// <param name="predicate"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">The predicate contains an expression that cannot be translated into a Postgrest filter, or never matches any row.</exception>
    IPostgrestTable<TModel> Where(Expression<Func<TModel, bool>> predicate);

    /// <summary>
    /// Executes a query that returns at most one row, as a single model rather than a list.
    /// Returns null when the query matches no rows. To have no match throw instead, use <see cref="SingleOrThrowAsync"/>.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <exception cref="Exceptions.PostgrestException">The query matches more than one row.</exception>
    /// <returns></returns>
    Task<TModel?> Single(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Postgres execution plan of the read instead of its rows, as the JSON output of
    /// <c>EXPLAIN (FORMAT JSON)</c>: an array holding one plan object, whose <c>Plan</c> is the root node.
    /// Filters, ordering, <see cref="Limit"/>, <see cref="Offset"/> and <see cref="Range(int, int)"/> shape the
    /// planned query as they do for <see cref="Get"/>.
    /// </summary>
    /// <remarks>
    /// PostgREST serves plans only when its <c>db-plan-enabled</c> setting is on, which is not the default: run
    /// <c>alter role authenticator set pgrst.db_plan_enabled to 'true'; notify pgrst, 'reload config';</c>.
    /// A plan reveals details of the database, so enable it outside production, or restrict it with a
    /// pre-request function. The root node is the aggregate PostgREST wraps around the query.
    /// </remarks>
    /// <param name="options">The extra details to include. None by default.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The plan, as Postgres reports it.</returns>
    /// <exception cref="Exceptions.PostgrestException">PostgREST does not serve plans
    /// (<see cref="Exceptions.FailureHint.Reason.MediaTypeNotAcceptable"/>), or refuses the request.</exception>
    /// <example>
    /// <code>
    /// var plan = await client.Table&lt;Movie&gt;()
    ///     .Filter("name", Operator.Equals, "Alien")
    ///     .ExplainAsync(new ExplainOptions { Analyze = true });
    /// var root = plan[0].GetProperty("Plan");
    /// Console.WriteLine($"{root.GetProperty("Node Type")} in {plan[0].GetProperty("Execution Time")} ms");
    /// </code>
    /// </example>
    Task<JsonElement> ExplainAsync(ExplainOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a read that must match exactly one row, and returns that row. The server enforces the count
    /// by answering with a single object, so a query that matches no row or several rows throws.
    /// </summary>
    /// <remarks>
    /// This is the strict twin of <see cref="Single"/>, which is the lenient "at most one row" read: it
    /// returns null for no match. In v9, <c>Single</c> becomes the strict one and <c>MaybeSingle</c> the lenient one.
    /// Only reads are supported: a write takes its row expectation from <see cref="QueryOptions"/>, and an RPC call is terminal.
    /// </remarks>
    /// <param name="cancellationToken"></param>
    /// <returns>The only row the query matches.</returns>
    /// <exception cref="Exceptions.PostgrestException">The query matches no row or more than one row (<see cref="Exceptions.FailureHint.Reason.UnexpectedRowCount"/>).</exception>
    /// <example>
    /// <code>
    /// var todo = await client.Table&lt;Todo&gt;().Filter("id", Operator.Equals, "1").SingleOrThrowAsync();
    /// </code>
    /// </example>
    Task<TModel> SingleOrThrowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Specifies a key and value to be updated. Should be combined with filters/where clauses.
    /// 
    /// Can be called multiple times to set multiple values.
    /// </summary>
    /// <param name="keySelector"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    IPostgrestTable<TModel> Set(Expression<Func<TModel, object>> keySelector, object? value);

    /// <summary>
    /// Specifies a KeyValuePair to be updated. Should be combined with filters/where clauses.
    /// 
    /// Can be called multiple times to set multiple values.
    /// </summary>
    /// <param name="keyValuePairExpression"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    IPostgrestTable<TModel> Set(Expression<Func<TModel, KeyValuePair<object, object?>>> keyValuePairExpression);

    /// <summary>
    /// Sets the maximum number of rows an <c>Update</c> or <c>Delete</c> may affect. When the mutation would
    /// affect more rows, PostgREST rolls it back and the call throws. Needs PostgREST 13 or later: an older
    /// server ignores the limit.
    /// </summary>
    /// <param name="rows">The most rows the mutation may affect; zero forbids any affected row.</param>
    /// <returns>The same table, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rows"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The terminal call is not an Update or a Delete.</exception>
    /// <exception cref="Exceptions.PostgrestException">The mutation would affect more than <paramref name="rows"/> rows (<see cref="Exceptions.FailureHint.Reason.MaxAffectedExceeded"/>).</exception>
    /// <example>
    /// <code>
    /// await client.Table&lt;Todo&gt;().Filter("done", Operator.Equals, "true").MaxAffected(10).Delete();
    /// </code>
    /// </example>
    IPostgrestTable<TModel> MaxAffected(int rows);

    /// <summary>
    /// Leaves null values out of the returned rows. Needs PostgREST 11.2 or later.
    /// </summary>
    IPostgrestTable<TModel> StripNulls();

    /// <summary>
    /// Calls an Update function after `Set` has been called.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    Task<ModeledResponse<TModel>> Update(QueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an UPDATE query using the defined query params on the current instance.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>A typed response from the database.</returns>
    Task<ModeledResponse<TModel>> Update(TModel model, QueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an UPSERT query using the defined query params on the current instance.
    ///
    /// By default the new record is returned. Set QueryOptions.ReturnType to Minimal if you don't need this value.
    /// By specifying the QueryOptions.OnConflict parameter, you can make UPSERT work on a column(s) that has a UNIQUE constraint.
    /// QueryOptions.DuplicateResolution.IgnoreDuplicates Specifies if duplicate rows should be ignored and not inserted.
    /// Rows may carry different fields: the request names the union of the fields in the `columns` query parameter,
    /// unless <see cref="Columns(string[])"/> was called.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ModeledResponse<TModel>> Upsert(ICollection<TModel> model, QueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an UPSERT query using the defined query params on the current instance.
    /// 
    /// By default the new record is returned. Set QueryOptions.ReturnType to Minimal if you don't need this value.
    /// By specifying the QueryOptions.OnConflict parameter, you can make UPSERT work on a column(s) that has a UNIQUE constraint.
    /// QueryOptions.DuplicateResolution.IgnoreDuplicates Specifies if duplicate rows should be ignored and not inserted.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ModeledResponse<TModel>> Upsert(TModel model, QueryOptions? options = null,
        CancellationToken cancellationToken = default);
}
