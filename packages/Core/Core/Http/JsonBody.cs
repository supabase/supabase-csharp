using System;
using System.Text.Json;

namespace Supabase.Core.Http;

/// <summary>Deserializes JSON response bodies.</summary>
public static class JsonBody
{
    /// <summary>Deserializes the body, wrapping JSON errors with the supplied exception factory.</summary>
    public static T? Deserialize<T>(string body, JsonSerializerOptions options, Func<JsonException, Exception> exceptionFactory)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, options);
        }
        catch (JsonException e)
        {
            throw exceptionFactory(e);
        }
    }
}
