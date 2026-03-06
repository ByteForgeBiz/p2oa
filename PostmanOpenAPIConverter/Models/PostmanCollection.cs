using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostmanOpenAPIConverter.Models;

/// <summary>
/// Represents a Postman collection (v2.0 or v2.1) with all its metadata, items, and configuration.
/// </summary>
public class PostmanCollection
{
    /// <summary>
    /// Gets or sets the collection metadata including name, description, and schema version.
    /// </summary>
    [JsonPropertyName("info")]
    public PostmanInfo Info { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of items (folders and requests) in this collection.
    /// </summary>
    [JsonPropertyName("item")]
    public List<PostmanItem> Item { get; set; } = [];

    /// <summary>
    /// Gets or sets collection-level variables (e.g. baseUrl).
    /// </summary>
    [JsonPropertyName("variable")]
    public List<PostmanVariable>? Variable { get; set; }

    /// <summary>
    /// Gets or sets collection-level events (pre-request or test scripts).
    /// </summary>
    [JsonPropertyName("event")]
    public List<PostmanEvent>? Event { get; set; }

    /// <summary>
    /// Gets or sets collection-level authentication configuration.
    /// </summary>
    [JsonPropertyName("auth")]
    public PostmanAuth? Auth { get; set; }
}

/// <summary>
/// Collection metadata including name, description, and schema version.
/// </summary>
public class PostmanInfo
{
    /// <summary>
    /// Gets or sets the name of the collection.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the description of the collection (plain text or markdown in v2.1).
    /// </summary>
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
    /// <summary>
    /// Gets or sets the name of this item (folder or request).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the description of this item.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the HTTP request (if this item is a request, not a folder).
    /// </summary>
    [JsonPropertyName("request")]
    public PostmanRequest? Request { get; set; }

    /// <summary>
    /// Gets or sets the child items (if this item is a folder, not a request).
    /// </summary>
    [JsonPropertyName("item")]
    public List<PostmanItem>? Item { get; set; }

    /// <summary>
    /// Gets or sets the events (scripts) attached to this item.
    /// </summary>
    [JsonPropertyName("event")]
    public List<PostmanEvent>? Event { get; set; }

    /// <summary>
    /// Gets or sets the authentication configuration for this item.
    /// </summary>
    [JsonPropertyName("auth")]
    public PostmanAuth? Auth { get; set; }
}

/// <summary>
/// Represents an HTTP request with method, URL, headers, body, and authentication.
/// </summary>
public class PostmanRequest
{
    /// <summary>
    /// Gets or sets the HTTP method (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Gets or sets the list of HTTP headers for this request.
    /// </summary>
    [JsonPropertyName("header")]
    public List<PostmanHeader>? Header { get; set; }

    /// <summary>
    /// Gets or sets the URL for this request (can be a string or structured URL object).
    /// </summary>
    [JsonPropertyName("url")]
    [JsonConverter(typeof(PostmanUrlConverter))]
    public PostmanUrl? Url { get; set; }

    /// <summary>
    /// Gets or sets the request body (raw, urlencoded, or formdata).
    /// </summary>
    [JsonPropertyName("body")]
    public PostmanBody? Body { get; set; }

    /// <summary>
    /// Gets or sets the description of this request.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the authentication configuration for this request.
    /// </summary>
    [JsonPropertyName("auth")]
    public PostmanAuth? Auth { get; set; }
}

/// <summary>
/// Represents an HTTP header with a key-value pair and optional disabled flag.
/// </summary>
public class PostmanHeader
{
    /// <summary>
    /// Gets or sets the header name (e.g., "Content-Type").
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>
    /// Gets or sets the header value (e.g., "application/json").
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    /// <summary>
    /// Gets or sets a value indicating whether this header is disabled.
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }
}

/// <summary>
/// Represents a Postman URL with raw string, host, path segments, query parameters, and path variables.
/// </summary>
public class PostmanUrl
{
    /// <summary>
    /// Gets or sets the raw URL string.
    /// </summary>
    public string Raw { get; set; } = "";

    /// <summary>
    /// Gets or sets the host segments (e.g., ["api", "example", "com"]).
    /// </summary>
    public List<string>? Host { get; set; }

    /// <summary>
    /// Path segments. Each entry is a plain string or, in some edge cases, an object
    /// with a <c>value</c> key – the <see cref="PostmanUrlConverter"/> normalises both.
    /// </summary>
    public List<string>? Path { get; set; }

    /// <summary>
    /// Gets or sets the query parameters for this URL.
    /// </summary>
    public List<PostmanQueryParam>? Query { get; set; }

    /// <summary>Named path-variable bindings (v2.1). Provides descriptions used in OpenAPI.</summary>
    public List<PostmanVariable>? Variable { get; set; }
}

/// <summary>
/// Represents a query parameter with key, value, description, and optional disabled flag.
/// </summary>
public class PostmanQueryParam
{
    /// <summary>
    /// Gets or sets the query parameter name.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>
    /// Gets or sets the query parameter value.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this query parameter is disabled.
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }

    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }
}

/// <summary>
/// Represents a variable (collection-level or path-level) with key, value, and description.
/// </summary>
public class PostmanVariable
{
    /// <summary>
    /// Gets or sets the variable name/key.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>
    /// Gets or sets the variable value.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("description")]
    [JsonConverter(typeof(DescriptionConverter))]
    public string? Description { get; set; }
}

/// <summary>
/// Represents the request body which can be raw text, form data, or URL-encoded data.
/// </summary>
public class PostmanBody
{
    /// <summary>
    /// Gets or sets the body mode ("raw", "urlencoded", "formdata", etc.).
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>
    /// Gets or sets the raw body content (when mode is "raw").
    /// </summary>
    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    /// <summary>
    /// Gets or sets additional options for the body (e.g., language hints for raw content).
    /// </summary>
    [JsonPropertyName("options")]
    public PostmanBodyOptions? Options { get; set; }

    /// <summary>
    /// Gets or sets the URL-encoded body data (when mode is "urlencoded").
    /// </summary>
    [JsonPropertyName("urlencoded")]
    public List<PostmanKeyValuePair>? Urlencoded { get; set; }

    /// <summary>
    /// Gets or sets the form data body (when mode is "formdata").
    /// </summary>
    [JsonPropertyName("formdata")]
    public List<PostmanKeyValuePair>? Formdata { get; set; }
}

/// <summary>
/// Additional options for request body, such as language hints for raw content.
/// </summary>
public class PostmanBodyOptions
{
    /// <summary>
    /// Gets or sets options specific to raw body content.
    /// </summary>
    [JsonPropertyName("raw")]
    public PostmanBodyRawOptions? Raw { get; set; }
}

/// <summary>
/// Options specific to raw body content, including language identification.
/// </summary>
public class PostmanBodyRawOptions
{
    /// <summary>
    /// Gets or sets the language/format of the raw content ("json", "xml", "text", etc.).
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

/// <summary>
/// Generic key-value pair used in URL-encoded and form-data bodies.
/// </summary>
public class PostmanKeyValuePair
{
    /// <summary>
    /// Gets or sets the key/name of this parameter.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>
    /// Gets or sets the value of this parameter.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the type for formdata ("text" or "file").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this parameter is disabled.
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }
}

/// <summary>
/// Represents a pre-request or test script event attached to a collection or request.
/// </summary>
public class PostmanEvent
{
    /// <summary>
    /// Gets or sets the event type ("prerequest" or "test").
    /// </summary>
    [JsonPropertyName("listen")]
    public string Listen { get; set; } = "";

    /// <summary>
    /// Gets or sets the script to execute for this event.
    /// </summary>
    [JsonPropertyName("script")]
    public PostmanScript? Script { get; set; }
}

/// <summary>
/// Contains script code and metadata for pre-request or test scripts.
/// </summary>
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
