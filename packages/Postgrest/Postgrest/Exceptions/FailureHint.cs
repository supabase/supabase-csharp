using static Supabase.Postgrest.Exceptions.FailureHint.Reason;
#pragma warning disable CS1591

namespace Supabase.Postgrest.Exceptions;


/// <summary>
/// https://postgrest.org/en/v10.2/errors.html?highlight=exception#http-status-codes
/// </summary>
public static class FailureHint
{
    public enum Reason
    {
        Unknown,
        NotAuthorized,
        ForeignKeyViolation,
        UniquenessViolation,
        ServerError,
        UndefinedTable,
        UndefinedFunction,
        InvalidArgument,
        UriTooLong
    }

    public static Reason DetectReason(PostgrestException pgex)
    {
        if (pgex.Content == null)
            return Unknown;

        return pgex.StatusCode switch
        {
            401 => NotAuthorized,
            403 when pgex.Content.Contains("apikey") => NotAuthorized,
            404 when pgex.Content.Contains("42883") => UndefinedTable,
            404 when pgex.Content.Contains("42P01") => UndefinedFunction,
            409 when pgex.Content.Contains("23503") => ForeignKeyViolation,
            409 when pgex.Content.Contains("23505") => UniquenessViolation,
            414 => UriTooLong,
            500 => ServerError,
            _ => Unknown
        };
    }
}
