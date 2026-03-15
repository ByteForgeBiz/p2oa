using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

// YamlDotNet deserializes all YAML mappings as Dictionary<object, object>
using Yaml = System.Collections.Generic.Dictionary<object, object>;

namespace PostmanOpenAPIConverter.Converters;

/// <summary>
/// Converts Postman GIT-compatible YAML directory structures back to Postman collection JSON format.
/// </summary>
public static class PostmanGitYamlToJsonConverter
{
    /// <summary>
    /// YAML deserializer for reading Postman GIT YAML files.
    /// </summary>
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    /// <summary>
    /// JSON serializer options configured for writing formatted output.
    /// </summary>
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // ── Entry points ─────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a Postman GIT directory to a Postman collection JSON string.
    /// <paramref name="inputDir"/> may be the repo root (containing
    /// <c>postman/collections/</c>) or the collection directory itself.
    /// </summary>
    /// <param name="inputDir">The input directory containing the Postman GIT structure.</param>
    /// <param name="collectionName">Optional collection name to convert when multiple collections exist.</param>
    /// <returns>The Postman collection as a JSON string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the collection structure cannot be found or parsed.</exception>
    public static string Convert(DirectoryInfo inputDir, string? collectionName = null)
    {
        var collectionDir = FindCollectionDir(inputDir, collectionName);
        return ConvertCollection(collectionDir);
    }

    // ── Directory discovery ───────────────────────────────────────────────────

    /// <summary>
    /// Locates the collection directory within the input directory structure.
    /// </summary>
    /// <param name="inputDir">The input directory to search.</param>
    /// <param name="collectionName">Optional collection name to find.</param>
    /// <returns>The directory containing the collection definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the collection directory cannot be found.</exception>
    internal static DirectoryInfo FindCollectionDir(DirectoryInfo inputDir, string? collectionName)
    {
        // Is inputDir itself the collection directory?
        if (File.Exists(Path.Combine(inputDir.FullName, ".resources", "definition.yaml")))
            return inputDir;

        // Look under postman/collections/
        var collectionsDir = new DirectoryInfo(
            Path.Combine(inputDir.FullName, "postman", "collections"));

        if (!collectionsDir.Exists)
            throw new InvalidOperationException(
                "Cannot locate a Postman GIT collection. Expected either a collection directory " +
                "(with .resources/definition.yaml) or a repo root containing postman/collections/.");

        var collections = collectionsDir.GetDirectories();
        if (collections.Length == 0)
            throw new InvalidOperationException("No collections found under postman/collections/.");

        if (collectionName is not null)
        {
            var match = collections.FirstOrDefault(d =>
                string.Equals(d.Name, collectionName, StringComparison.OrdinalIgnoreCase));
            return match is null
                ? throw new InvalidOperationException(
                    $"Collection '{collectionName}' not found. " +
                    $"Available: {string.Join(", ", collections.Select(d => d.Name))}")
                : match;
        }

        if (collections.Length > 1)
            throw new InvalidOperationException(
                $"Multiple collections found — specify one with --collection: " +
                string.Join(", ", collections.Select(d => d.Name)));

        return collections[0];
    }

    // ── Collection builder ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a Postman collection JSON object from a collection directory.
    /// </summary>
    /// <param name="collectionDir">The directory containing the collection definition and items.</param>
    /// <returns>The Postman collection as a JSON string.</returns>
    internal static string ConvertCollection(DirectoryInfo collectionDir)
    {
        var def = ReadYaml(Path.Combine(collectionDir.FullName, ".resources", "definition.yaml"));

        var info = new JsonObject
        {
            ["_postman_id"] = Guid.NewGuid().ToString(),
            ["name"]        = Str(def, "name") ?? collectionDir.Name,
            ["description"] = Str(def, "description"),
            ["schema"]      = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
        };
        RemoveNulls(info);

        var collection = new JsonObject
        {
            ["info"]     = info,
            ["item"]     = ReadItems(collectionDir),
            ["variable"] = BuildVariablesJson(def),
            ["event"]    = BuildScriptsJson(def),
            ["auth"]     = BuildAuthJson(def)
        };
        RemoveNulls(collection);

        return collection.ToJsonString(WriteOptions);
    }

    // ── Item tree reader ──────────────────────────────────────────────────────

    /// <summary>
    /// Recursively reads items (requests and folders) from a directory.
    /// </summary>
    /// <param name="dir">The directory to read items from.</param>
    /// <returns>A JSON array containing the items in their proper order.</returns>
    internal static JsonArray ReadItems(DirectoryInfo dir)
    {
        var items = new List<(long Order, JsonObject Item)>();

        // Request files: {Name}.request.yaml
        foreach (var file in dir.GetFiles("*.request.yaml"))
        {
            var yaml = ReadYaml(file.FullName);

            // "Get Campaign.request.yaml" → GetFileNameWithoutExtension → "Get Campaign.request"
            // then strip the trailing ".request" suffix
            var stem = Path.GetFileNameWithoutExtension(file.Name);
            var name = stem.EndsWith(".request", StringComparison.OrdinalIgnoreCase)
                ? stem[..^".request".Length]
                : stem;

            items.Add((Long(yaml, "order"), BuildRequestItem(name, yaml)));
        }

        // Subdirectories = folders (skip .resources)
        foreach (var subDir in dir.GetDirectories().Where(d => d.Name != ".resources"))
        {
            var defPath = Path.Combine(subDir.FullName, ".resources", "definition.yaml");
            var folderDef = File.Exists(defPath) ? ReadYaml(defPath) : [];

            items.Add((Long(folderDef, "order"), BuildFolderItem(subDir, folderDef)));
        }

        var result = new JsonArray();
        foreach (var (_, item) in items.OrderBy(x => x.Order))
            result.Add(item);
        return result;
    }

    // ── Item builders ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a Postman request item from a YAML request definition.
    /// </summary>
    /// <param name="name">The name of the request.</param>
    /// <param name="yaml">The YAML dictionary containing request data.</param>
    /// <returns>A JSON object representing the Postman request item.</returns>
    internal static JsonObject BuildRequestItem(string name, Yaml yaml)
    {
        var request = new JsonObject
        {
            ["method"]      = Str(yaml, "method") ?? "GET",
            ["url"]         = BuildUrlJson(yaml),
            ["header"]      = BuildHeadersJson(yaml),
            ["body"]        = BuildBodyJson(yaml),
            ["auth"]        = BuildAuthJson(yaml),
            ["description"] = Str(yaml, "description")
        };
        RemoveNulls(request);

        var item = new JsonObject
        {
            ["name"]     = name,
            ["request"]  = request,
            ["event"]    = BuildScriptsJson(yaml),
            ["response"] = new JsonArray()
        };
        RemoveNulls(item);
        return item;
    }

    /// <summary>
    /// Builds a Postman folder item from a subdirectory.
    /// </summary>
    /// <param name="subDir">The subdirectory containing the folder.</param>
    /// <param name="folderDef">The YAML dictionary containing folder definition data.</param>
    /// <returns>A JSON object representing the Postman folder item.</returns>
    internal static JsonObject BuildFolderItem(DirectoryInfo subDir, Yaml folderDef)
    {
        var item = new JsonObject
        {
            ["name"]        = Str(folderDef, "name") ?? subDir.Name,
            ["description"] = Str(folderDef, "description"),
            ["item"]        = ReadItems(subDir),
            ["event"]       = BuildScriptsJson(folderDef)
        };
        RemoveNulls(item);
        return item;
    }

    // ── Field builders ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a Postman URL JSON object from YAML data.
    /// </summary>
    /// <param name="yaml">The YAML dictionary containing URL data.</param>
    /// <returns>A JSON object representing the Postman URL.</returns>
    internal static JsonObject BuildUrlJson(Yaml yaml)
    {
        var urlObj = new JsonObject { ["raw"] = Str(yaml, "url") ?? "" };

        if (yaml.TryGetValue("queryParams", out var qp) && qp is Yaml qpDict)
        {
            var arr = new JsonArray();
            foreach (var (k, v) in qpDict)
                arr.Add(new JsonObject { ["key"] = k.ToString(), ["value"] = v?.ToString() ?? "" });
            urlObj["query"] = arr;
        }

        if (yaml.TryGetValue("pathVariables", out var pv) && pv is Yaml pvDict)
        {
            var arr = new JsonArray();
            foreach (var (k, v) in pvDict)
                arr.Add(new JsonObject { ["key"] = k.ToString(), ["value"] = v?.ToString() ?? "" });
            urlObj["variable"] = arr;
        }

        return urlObj;
    }

    /// <summary>
    /// Builds a Postman headers JSON array from YAML data.
    /// </summary>
    /// <param name="yaml">The YAML dictionary containing headers data.</param>
    /// <returns>A JSON array representing the Postman headers.</returns>
    internal static JsonArray BuildHeadersJson(Yaml yaml)
    {
        var arr = new JsonArray();
        if (yaml.TryGetValue("headers", out var h) && h is Yaml headers)
            foreach (var (k, v) in headers)
                arr.Add(new JsonObject { ["key"] = k.ToString(), ["value"] = v?.ToString() ?? "" });
        return arr;
    }

    /// <summary>
    /// Builds a Postman body JSON object from YAML data.
    /// </summary>
    /// <param name="yaml">The YAML dictionary containing body data.</param>
    /// <returns>A JSON object representing the Postman body, or null if no body exists.</returns>
    internal static JsonObject? BuildBodyJson(Yaml yaml)
    {
        if (!yaml.TryGetValue("body", out var b) || b is not Yaml body) return null;

        var type = Str(body, "type") ?? "raw";
        body.TryGetValue("content", out var content);

        return type switch
        {
            "urlencoded" => BuildUrlencodedBody(content),
            "formdata"   => BuildFormdataBody(content),
            _            => BuildRawBody(type, content)
        };
    }

    /// <summary>
    /// Builds a raw body JSON object (json, xml, text, etc.).
    /// </summary>
    /// <param name="type">The body type (json, xml, html, text, raw).</param>
    /// <param name="content">The body content.</param>
    /// <returns>A JSON object representing the raw body.</returns>
    internal static JsonObject BuildRawBody(string type, object? content)
    {
        var body = new JsonObject
        {
            ["mode"] = "raw",
            ["raw"]  = content?.ToString() ?? ""
        };
        if (type != "raw")
            body["options"] = new JsonObject
            {
                ["raw"] = new JsonObject { ["language"] = type }
            };
        return body;
    }

    /// <summary>
    /// Builds a URL-encoded body JSON object.
    /// </summary>
    /// <param name="content">The body content dictionary.</param>
    /// <returns>A JSON object representing the URL-encoded body.</returns>
    internal static JsonObject BuildUrlencodedBody(object? content)
    {
        var arr = new JsonArray();
        if (content is Yaml dict)
            foreach (var (k, v) in dict)
                arr.Add(new JsonObject
                {
                    ["key"]   = k.ToString(),
                    ["value"] = v?.ToString() ?? "",
                    ["type"]  = "text"
                });
        return new JsonObject { ["mode"] = "urlencoded", ["urlencoded"] = arr };
    }

    /// <summary>
    /// Builds a form-data body JSON object.
    /// </summary>
    /// <param name="content">The body content list.</param>
    /// <returns>A JSON object representing the form-data body.</returns>
    internal static JsonObject BuildFormdataBody(object? content)
    {
        var arr = new JsonArray();
        if (content is List<object> list)
            foreach (var item in list.OfType<Yaml>())
                arr.Add(new JsonObject
                {
                    ["key"]   = Str(item, "key") ?? "",
                    ["value"] = Str(item, "value") ?? "",
                    ["type"]  = Str(item, "type") ?? "text"
                });
        return new JsonObject { ["mode"] = "formdata", ["formdata"] = arr };
    }

    /// <summary>
    /// Builds a Postman authentication JSON object from YAML data.
    /// </summary>
    /// <param name="yaml">The YAML dictionary containing authentication data.</param>
    /// <returns>A JSON object representing the Postman authentication, or null if no auth exists.</returns>
    internal static JsonObject? BuildAuthJson(Yaml yaml)
    {
        if (!yaml.TryGetValue("auth", out var a) || a is not Yaml auth) return null;

        var type = Str(auth, "type");
        if (type is null) return null;

        var authJson = new JsonObject { ["type"] = type };

        if (auth.TryGetValue("credentials", out var c) && c is Yaml creds)
        {
            var arr = new JsonArray();
            foreach (var (k, v) in creds)
                arr.Add(new JsonObject { ["key"] = k.ToString(), ["value"] = v?.ToString() ?? "" });
            authJson[type] = arr;
        }

        return authJson;
    }

    /// <summary>
    /// Builds a Postman scripts (events) JSON array from YAML data.
    /// </summary>
    /// <param name="yaml">The YAML dictionary containing scripts data.</param>
    /// <returns>A JSON array representing the Postman events, or null if no scripts exist.</returns>
    internal static JsonArray? BuildScriptsJson(Yaml yaml)
    {
        if (!yaml.TryGetValue("scripts", out var s) || s is not List<object> scripts)
            return null;

        var events = new JsonArray();
        foreach (var obj in scripts.OfType<Yaml>())
        {
            var scriptType = Str(obj, "type") ?? "afterResponse";
            var listen     = scriptType == "http:beforeRequest" ? "prerequest" : "test";
            var language   = Str(obj, "language") ?? "text/javascript";
            var code       = Str(obj, "code") ?? "";

            events.Add(new JsonObject
            {
                ["listen"] = listen,
                ["script"] = new JsonObject
                {
                    ["type"] = language,
                    ["exec"] = JsonSerializer.SerializeToNode(code.Split('\n').ToArray())
                }
            });
        }

        return events.Count > 0 ? events : null;
    }

    /// <summary>
    /// Builds a Postman variables JSON array from YAML data.
    /// </summary>
    /// <param name="yaml">The YAML dictionary containing variables data.</param>
    /// <returns>A JSON array representing the Postman variables, or null if no variables exist.</returns>
    internal static JsonArray? BuildVariablesJson(Yaml yaml)
    {
        if (!yaml.TryGetValue("variables", out var v) || v is not Yaml vars) return null;

        var arr = new JsonArray();
        foreach (var (k, val) in vars)
            arr.Add(new JsonObject { ["key"] = k.ToString(), ["value"] = val?.ToString() ?? "" });
        return arr.Count > 0 ? arr : null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads and deserializes a YAML file.
    /// </summary>
    /// <param name="path">The path to the YAML file.</param>
    /// <returns>A dictionary representing the YAML content.</returns>
    internal static Yaml ReadYaml(string path)
        => YamlDeserializer.Deserialize<Yaml>(File.ReadAllText(path)) ?? [];

    /// <summary>
    /// Safely retrieves a string value from a YAML dictionary.
    /// </summary>
    /// <param name="dict">The YAML dictionary.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The string value if found; otherwise, null.</returns>
    internal static string? Str(Yaml dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;

    /// <summary>
    /// Safely retrieves a long integer value from a YAML dictionary.
    /// </summary>
    /// <param name="dict">The YAML dictionary.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The long value if found and parseable; otherwise, 0.</returns>
    internal static long Long(Yaml dict, string key)
        => dict.TryGetValue(key, out var v) && long.TryParse(v?.ToString(), out var n) ? n : 0L;

    /// <summary>
    /// Removes all null-valued properties from a JSON object.
    /// </summary>
    /// <param name="obj">The JSON object to clean.</param>
    internal static void RemoveNulls(JsonObject obj)
    {
        foreach (var key in obj.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList())
            obj.Remove(key);
    }
}
