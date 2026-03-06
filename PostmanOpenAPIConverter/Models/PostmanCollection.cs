using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostmanOpenAPIConverter.Models;

public class PostmanCollection
{
    [JsonPropertyName("info")]
    public PostmanInfo Info { get; set; } = new();

    [JsonPropertyName("item")]
    public List<PostmanItem> Item { get; set; } = [];

    /// <summary>Collection-level variables (e.g. baseUrl).</summary>
    [JsonPropertyName("variable")]
    public List<PostmanVariable>? Variable { get; set; }

    [JsonPropertyName("event")]
    public List<PostmanEvent>? Event { get; set; }

    [JsonPropertyName("auth")]
    public PostmanAuth? Auth { get; set; }
}

public class PostmanInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }

    /// <summary>
    /// Schema URL identifies the collection version:
    ///   v2.0 – .../collection/v2.0.0/collection.json
    ///   v2.1 – .../collection/v2.1.0/collection.json
    /// </summary>
    [JsonPropertyName("schema")]
    public string? Schema { get; set; }
}

/// <summary>
/// A Postman item is either a folder (has <see cref="Item"/>) or a request (has <see cref="Request"/>).
/// </summary>
public class PostmanItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }

    [JsonPropertyName("request")]
    public PostmanRequest? Request { get; set; }

    [JsonPropertyName("item")]
    public List<PostmanItem>? Item { get; set; }

    [JsonPropertyName("event")]
    public List<PostmanEvent>? Event { get; set; }

    [JsonPropertyName("auth")]
    public PostmanAuth? Auth { get; set; }
}

public class PostmanRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    [JsonPropertyName("header")]
    public List<PostmanHeader>? Header { get; set; }

    [JsonPropertyName("url")]
    [JsonConverter(typeof(PostmanUrlConverter))]
    public PostmanUrl? Url { get; set; }

    [JsonPropertyName("body")]
    public PostmanBody? Body { get; set; }

    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }

    [JsonPropertyName("auth")]
    public PostmanAuth? Auth { get; set; }
}

public class PostmanHeader
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }
}

public class PostmanUrl
{
    public string Raw { get; set; } = "";
    public List<string>? Host { get; set; }

    /// <summary>
    /// Path segments. Each entry is a plain string or, in some edge cases, an object
    /// with a <c>value</c> key – the <see cref="PostmanUrlConverter"/> normalises both.
    /// </summary>
    public List<string>? Path { get; set; }
    public List<PostmanQueryParam>? Query { get; set; }

    /// <summary>Named path-variable bindings (v2.1). Provides descriptions used in OpenAPI.</summary>
    public List<PostmanVariable>? Variable { get; set; }
}

public class PostmanQueryParam
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }

    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }
}

public class PostmanVariable
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }
}

public class PostmanBody
{
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    [JsonPropertyName("options")]
    public PostmanBodyOptions? Options { get; set; }

    [JsonPropertyName("urlencoded")]
    public List<PostmanKeyValuePair>? Urlencoded { get; set; }

    [JsonPropertyName("formdata")]
    public List<PostmanKeyValuePair>? Formdata { get; set; }
}

public class PostmanBodyOptions
{
    [JsonPropertyName("raw")]
    public PostmanBodyRawOptions? Raw { get; set; }
}

public class PostmanBodyRawOptions
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

public class PostmanKeyValuePair
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>For formdata: "text" or "file".</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }
}

public class PostmanEvent
{
    /// <summary>"prerequest" or "test".</summary>
    [JsonPropertyName("listen")]
    public string Listen { get; set; } = "";

    [JsonPropertyName("script")]
    public PostmanScript? Script { get; set; }
}

public class PostmanScript
{
    /// <summary>MIME type of the script, e.g. "text/javascript".</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Lines of code (each entry is one line).</summary>
    [JsonPropertyName("exec")]
    public List<string>? Exec { get; set; }
}

/// <summary>
/// Authentication configuration. The auth-type-specific credentials are stored in a
/// property named after the type (e.g. "bearer", "basic", "oauth2") and captured via
/// <see cref="Extra"/> to handle all types generically.
/// </summary>
public class PostmanAuth
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

// ── JSON converters ──────────────────────────────────────────────────────────

/// <summary>
/// Handles the Postman description field which is either a plain string or
/// {"content": "...", "type": "text/markdown"} in v2.1.
/// </summary>
internal sealed class DescriptionConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                    return doc.RootElement.TryGetProperty("content", out var content)
                        ? content.GetString()
                        : null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

/// <summary>
/// Handles the Postman url field which is either a plain string or a URL object.
/// Also normalises path-segment objects ({type, value}) to plain strings.
/// </summary>
internal sealed class PostmanUrlConverter : JsonConverter<PostmanUrl?>
{
    public override PostmanUrl? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return ParseFromString(reader.GetString() ?? "");
            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                    return ParseFromElement(doc.RootElement, options);
            default:
                reader.Skip();
                return null;
        }
    }

    private static PostmanUrl ParseFromString(string raw)
    {
        var url = new PostmanUrl { Raw = raw };

        var qIdx = raw.IndexOf('?');
        var pathPart = qIdx >= 0 ? raw[..qIdx] : raw;
        var queryPart = qIdx >= 0 ? raw[(qIdx + 1)..] : "";

        // Strip protocol so the first segment is the host
        var withoutProtocol = System.Text.RegularExpressions.Regex.Replace(pathPart, @"^https?://", "");
        url.Path = withoutProtocol.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (!string.IsNullOrEmpty(queryPart))
        {
            url.Query = queryPart.Split('&').Select(p =>
            {
                var eq = p.IndexOf('=');
                return new PostmanQueryParam
                {
                    Key = Uri.UnescapeDataString(eq >= 0 ? p[..eq] : p),
                    Value = eq >= 0 ? Uri.UnescapeDataString(p[(eq + 1)..]) : null
                };
            }).ToList();
        }

        return url;
    }

    private static PostmanUrl ParseFromElement(JsonElement el, JsonSerializerOptions options)
    {
        var url = new PostmanUrl();

        if (el.TryGetProperty("raw", out var raw))
            url.Raw = raw.GetString() ?? "";

        if (el.TryGetProperty("host", out var host))
            url.Host = host.Deserialize<List<string>>(options);

        if (el.TryGetProperty("path", out var path))
        {
            url.Path = [];
            foreach (var seg in path.EnumerateArray())
            {
                if (seg.ValueKind == JsonValueKind.String)
                    url.Path.Add(seg.GetString() ?? "");
                else if (seg.ValueKind == JsonValueKind.Object
                         && seg.TryGetProperty("value", out var val))
                    url.Path.Add(val.GetString() ?? "");
            }
        }

        if (el.TryGetProperty("query", out var query))
            url.Query = query.Deserialize<List<PostmanQueryParam>>(options);

        if (el.TryGetProperty("variable", out var variable))
            url.Variable = variable.Deserialize<List<PostmanVariable>>(options);

        return url;
    }

    public override void Write(Utf8JsonWriter writer, PostmanUrl? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value?.Raw);
}
