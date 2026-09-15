using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Supabase.Postgrest.Attributes;
using Supabase.Realtime.Converters;

namespace Supabase.Realtime;

/// <summary>
/// Builds the <see cref="JsonSerializerOptions" /> that replace the Newtonsoft <c>JsonSerializerSettings</c> +
/// <c>CustomContractResolver</c>. The custom converters are registered globally (System.Text.Json auto-wraps
/// them for nullable members), and a <see cref="DefaultJsonTypeInfoResolver" /> modifier applies the
/// column-name mapping and primary-key <c>ShouldSerialize</c> the resolver used to do in <c>CreateProperty</c>.
/// Unknown members are ignored by default (System.Text.Json parity with Newtonsoft's
/// <c>MissingMemberHandling.Ignore</c>).
/// </summary>
internal static class RealtimeSerializerOptions
{
    internal static JsonSerializerOptions Build(ClientOptions? options = null)
    {
        options ??= new ClientOptions();

        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(Modify);

        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNameCaseInsensitive = true,
            // Newtonsoft serialized public fields; System.Text.Json ignores them unless this is set. Several
            // socket response types expose public fields (e.g. PhoenixResponse.Status/Response) — without this
            // the join reply's status deserializes to null and Subscribe() never completes.
            IncludeFields = true,
            // WALRUS delivers every postgres_changes record value as a JSON string (e.g. "user_id":"1"), which
            // Newtonsoft coerced into the model's numeric property; System.Text.Json is strict without this.
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters =
            {
                new DateTimeConverter(options.DateTimeFormat),
                new DateTimeListConverter(options.DateTimeFormat),
                new IntArrayConverter(),
                new StringArrayConverter(),
                new PostgresNestedArrayConverter(),
                new ObjectToInferredTypesConverter(),
            },
        };
    }

    private static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is not MemberInfo member)
                continue;

            var columnAttribute = member.GetCustomAttribute<ColumnAttribute>();
            if (columnAttribute != null)
            {
                property.Name = columnAttribute.ColumnName;
                if (columnAttribute.NullValueHandling == JsonIgnoreCondition.WhenWritingNull)
                    property.ShouldSerialize = static (_, value) => value != null;
                continue;
            }

            var primaryKeyAttribute = member.GetCustomAttribute<PrimaryKeyAttribute>();
            if (primaryKeyAttribute != null)
            {
                property.Name = primaryKeyAttribute.ColumnName;
                var shouldInsert = primaryKeyAttribute.ShouldInsert;
                property.ShouldSerialize = (_, _) => shouldInsert;
            }
        }
    }
}
