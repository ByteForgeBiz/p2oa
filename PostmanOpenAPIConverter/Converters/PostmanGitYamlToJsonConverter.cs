using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

// YamlDotNet deserializes all YAML mappings as Dictionary<object, object>
using Yaml = System.Collections.Generic.Dictionary<object, object>;

namespace PostmanOpenAPIConverter.Converters;

public static class PostmanGitYamlToJsonConverter
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // ── Entry points ─────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a Postman GIT directory to a Postman collection JSON string.
    /// <paramref name="inputDir"/> may be the repo root (containing
    /// <c>postman/collections/</c>) or the collection directory itself.
    /// </summary>
    public static string Convert(DirectoryInfo inputDir, string? collectionName = null)
    {
        var collectionDir = FindCollectionDir(inputDir, collectionName);
        return ConvertCollection(collectionDir);
    }

    // ── Directory discovery ───────────────────────────────────────────────────

    private static DirectoryInfo FindCollectionDir(DirectoryInfo inputDir, string? collectionName)
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
            if (match is null)
                throw new InvalidOperationException(
                    $"Collection '{collectionName}' not found. " +
                    $"Available: {string.Join(", ", collections.Select(d => d.Name))}");
            return match;
        }

        if (collections.Length > 1)
            throw new InvalidOperationException(
                $"Multiple collections found — specify one with --collection: " +
                string.Join(", ", collections.Select(d => d.Name)));

        return collections[0];
    }

    // ── Collection builder ────────────────────────────────────────────────────

    private static string ConvertCollection(DirectoryInfo collectionDir)
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

    private static JsonArray ReadItems(DirectoryInfo dir)
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

    private static JsonObject BuildRequestItem(string name, Yaml yaml)
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

    private static JsonObject BuildFolderItem(DirectoryInfo subDir, Yaml folderDef)
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

    private static JsonNode BuildUrlJson(Yaml yaml)
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

    private static JsonArray BuildHeadersJson(Yaml yaml)
    {
        var arr = new JsonArray();
        if (yaml.TryGetValue("headers", out var h) && h is Yaml headers)
            foreach (var (k, v) in headers)
                arr.Add(new JsonObject { ["key"] = k.ToString(), ["value"] = v?.ToString() ?? "" });
        return arr;
    }

    private static JsonObject? BuildBodyJson(Yaml yaml)
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

    private static JsonObject BuildRawBody(string type, object? content)
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

    private static JsonObject BuildUrlencodedBody(object? content)
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

    private static JsonObject BuildFormdataBody(object? content)
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

    private static JsonObject? BuildAuthJson(Yaml yaml)
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

    private static JsonArray? BuildScriptsJson(Yaml yaml)
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

    private static JsonArray? BuildVariablesJson(Yaml yaml)
    {
        if (!yaml.TryGetValue("variables", out var v) || v is not Yaml vars) return null;

        var arr = new JsonArray();
        foreach (var (k, val) in vars)
            arr.Add(new JsonObject { ["key"] = k.ToString(), ["value"] = val?.ToString() ?? "" });
        return arr.Count > 0 ? arr : null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Yaml ReadYaml(string path)
        => YamlDeserializer.Deserialize<Yaml>(File.ReadAllText(path)) ?? [];

    private static string? Str(Yaml dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static long Long(Yaml dict, string key)
        => dict.TryGetValue(key, out var v) && long.TryParse(v?.ToString(), out var n) ? n : 0L;

    private static void RemoveNulls(JsonObject obj)
    {
        foreach (var key in obj.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList())
            obj.Remove(key);
    }
}
