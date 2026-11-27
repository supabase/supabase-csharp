using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Supabase.Postgrest.Extensions;
using Supabase.Postgrest.Models;

namespace Supabase.Postgrest.Responses;


/// <summary>
/// A representation of a successful Postgrest response that transforms the string response into a C# Modelled response.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ModeledResponse<T> : BaseResponse where T : BaseModel, new()
{
    /// <summary>
    /// The first model in the response.
    /// </summary>
    public T? Model => this.Models.FirstOrDefault();

    /// <summary>
    /// A list of models in the response.
    /// </summary>
    public List<T> Models { get; } = new();

    /// <summary>
    /// The number of results matching the specified filters
    /// </summary>
    public int Count = 0;

    /// <inheritdoc />
    public ModeledResponse(BaseResponse baseResponse, JsonSerializerOptions serializerSettings, Func<Dictionary<string, string>>? getHeaders = null, bool shouldParse = true) : base(baseResponse.ClientOptions, baseResponse.ResponseMessage, baseResponse.Content)
    {
        this.Content = baseResponse.Content;
        this.ResponseMessage = baseResponse.ResponseMessage;

        if (!shouldParse || string.IsNullOrEmpty(this.Content)) return;

        var token = Helpers.DeserializeBody<JsonNode>(this, serializerSettings);

        switch (token)
        {
            // A List of models has been returned
            case JsonArray:
                {
                    var deserialized = JsonSerializer.Deserialize<List<T>>(this.Content!, serializerSettings);

                    if (deserialized != null)
                        this.Models = deserialized;

                    foreach (var model in this.Models)
                    {
                        model.AttachTo(baseResponse.ResponseMessage!.RequestMessage.RequestUri.GetInstanceUrl().Replace(model.TableName, "").TrimEnd('/'), this.ClientOptions, getHeaders);
                    }

                    break;
                }
            // A single model has been returned
            case JsonObject:
                {
                    this.Models.Clear();

                    var obj = JsonSerializer.Deserialize<T>(this.Content!, serializerSettings);

                    if (obj != null)
                    {
                        obj.AttachTo(baseResponse.ResponseMessage!.RequestMessage.RequestUri.GetInstanceUrl().Replace(obj.TableName, "").TrimEnd('/'), this.ClientOptions, getHeaders);

                        this.Models.Add(obj);
                    }

                    break;
                }
        }

        try
        {
            var countStr = baseResponse.ResponseMessage?.Content.Headers.GetValues("Content-Range")
                .FirstOrDefault();
            this.Count = int.Parse(countStr?.Split('/')[1] ?? throw new InvalidOperationException());
        }
        catch (Exception e)
        {
            Debugger.Instance.Log(this, e.Message);
            this.Count = -1;
        }

        Debugger.Instance.Log(this, $"Response: [{baseResponse.ResponseMessage?.StatusCode}]\n" + $"Parsed Models <{typeof(T).Name}>:\n\t{JsonSerializer.Serialize(this.Models, serializerSettings)}\n");
    }
}
