using System.Text.Json;
using PostmanOpenAPIConverter.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PostmanOpenAPIConverter.Converters;

public static class PostmanToGitYamlConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly ISerializer Yaml = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    // ── Entry points ─────────────────────────────────────────────────────────

    public static void Convert(string postmanJson, DirectoryInfo outputDir)
    {
        PostmanCollection collection;
        try
        {
            collection = JsonSerializer.Deserialize<PostmanCollection>(postmanJson, JsonOptions)
                ?? throw new InvalidOperationException("Postman collection deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse Postman collection: {ex.Message}", ex);
        }

        Convert(collection, outputDir);
    }

    public static void Convert(PostmanCollection collection, DirectoryInfo outputDir)
    {
        // .postman/resources.yaml — workspace binding
        WriteYaml(outputDir.CreateSubdirectory(".postman"), "resources.yaml",
            new GitResources { WorkspaceId = Guid.NewGuid().ToString(), LocalResources = true });

        // postman/globals/workspace.globals.yaml
        var postmanDir = outputDir.CreateSubdirectory("postman");
        WriteYaml(postmanDir.CreateSubdirectory("globals"), "workspace.globals.yaml",
            new GitGlobals { Name = "Globals" });

        // postman/collections/{CollectionName}/
        var collectionDir = postmanDir
            .CreateSubdirectory("collections")
            .CreateSubdirectory(SanitizeName(collection.Info.Name));

        WriteYaml(collectionDir.CreateSubdirectory(".resources"), "definition.yaml",
            new GitCollection
            {
                Name = collection.Info.Name,
                Description = collection.Info.Description,
                Variables = BuildVariables(collection.Variable),
                Scripts = BuildScripts(collection.Event)
            });

        ProcessItems(collection.Item, collectionDir);
    }

    // ── Tree traversal ────────────────────────────────────────────────────────

    private static void ProcessItems(List<PostmanItem> items, DirectoryInfo dir)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            long order = (long)(i + 1) * 1000;

            if (item.Request is not null)
            {
                WriteYaml(dir, SanitizeName(item.Name) + ".request.yaml",
                    BuildRequest(item, order));
            }
            else if (item.Item is not null)
            {
                var folderDir = dir.CreateSubdirectory(SanitizeName(item.Name));
                WriteYaml(folderDir.CreateSubdirectory(".resources"), "definition.yaml",
                    new GitCollection
                    {
                        Name = item.Name,
                        Description = item.Description,
                        Order = order,
                        Scripts = BuildScripts(item.Event)
                    });
                ProcessItems(item.Item, folderDir);
            }
        }
    }

    // ── Request builder ───────────────────────────────────────────────────────

    private static GitHttpRequest BuildRequest(PostmanItem item, long order)
    {
        var req = item.Request!;
        return new GitHttpRequest
        {
            Description = req.Description ?? item.Description,
            Url = req.Url?.Raw ?? "",
            Method = req.Method.ToUpperInvariant(),
            Headers = BuildHeaders(req),
            QueryParams = BuildQueryParams(req),
            PathVariables = BuildPathVariables(req),
            Body = BuildBody(req),
            Auth = BuildAuth(req.Auth),
            Scripts = BuildScripts(item.Event),
            Order = order
        };
    }

    // ── Field builders ────────────────────────────────────────────────────────

    private static Dictionary<string, string>? BuildVariables(List<PostmanVariable>? vars)
    {
        if (vars is not { Count: > 0 }) return null;
        var dict = vars
            .Where(v => !string.IsNullOrEmpty(v.Key))
            .ToDictionary(v => v.Key, v => v.Value ?? "");
        return dict.Count > 0 ? dict : null;
    }

    private static Dictionary<string, string>? BuildHeaders(PostmanRequest req)
    {
        if (req.Header is not { Count: > 0 }) return null;
        var dict = req.Header
            .Where(h => h.Disabled != true && !string.IsNullOrEmpty(h.Key))
            .ToDictionary(h => h.Key, h => h.Value);
        return dict.Count > 0 ? dict : null;
    }

    private static Dictionary<string, string>? BuildQueryParams(PostmanRequest req)
    {
        if (req.Url?.Query is not { Count: > 0 }) return null;
        var dict = req.Url.Query
            .Where(q => q.Disabled != true && !string.IsNullOrEmpty(q.Key))
            .ToDictionary(q => q.Key, q => q.Value ?? "");
        return dict.Count > 0 ? dict : null;
    }

    private static Dictionary<string, string>? BuildPathVariables(PostmanRequest req)
    {
        if (req.Url?.Variable is not { Count: > 0 }) return null;
        var dict = req.Url.Variable
            .Where(v => !string.IsNullOrEmpty(v.Key))
            .ToDictionary(v => v.Key, v => v.Value ?? "");
        return dict.Count > 0 ? dict : null;
    }

    private static GitBody? BuildBody(PostmanRequest req)
    {
        if (req.Body is null) return null;

        return req.Body.Mode switch
        {
            "raw" when req.Body.Raw is { Length: > 0 } =>
                new GitBody
                {
                    Type = ResolveRawBodyType(req),
                    Content = req.Body.Raw
                },

            "urlencoded" when req.Body.Urlencoded is { Count: > 0 } =>
                new GitBody
                {
                    Type = "urlencoded",
                    Content = req.Body.Urlencoded
                        .Where(kv => kv.Disabled != true && !string.IsNullOrEmpty(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value ?? "")
                },

            "formdata" when req.Body.Formdata is { Count: > 0 } =>
                new GitBody
                {
                    Type = "formdata",
                    Content = req.Body.Formdata
                        .Where(fd => fd.Disabled != true && !string.IsNullOrEmpty(fd.Key))
                        .Select(fd => new Dictionary<string, string>
                        {
                            ["key"] = fd.Key,
                            ["value"] = fd.Value ?? "",
                            ["type"] = fd.Type ?? "text"
                        })
                        .ToList()
                },

            _ => null
        };
    }

    private static string ResolveRawBodyType(PostmanRequest req)
    {
        if (req.Body?.Options?.Raw?.Language is { } lang)
            return lang; // "json", "xml", "html", "text"

        var raw = req.Body?.Raw?.TrimStart();
        if (raw is not null && (raw.StartsWith('{') || raw.StartsWith('[')))
            return "json";

        return "raw";
    }

    private static GitAuth? BuildAuth(PostmanAuth? auth)
    {
        if (auth?.Type is null) return null;

        // Credentials are stored under a property named after the auth type
        // e.g. "bearer": [{"key":"token","value":"..."}]
        if (auth.Extra?.TryGetValue(auth.Type, out var credsEl) == true
            && credsEl.ValueKind == JsonValueKind.Array)
        {
            var creds = credsEl.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("key", out _))
                .ToDictionary(
                    e => e.GetProperty("key").GetString() ?? "",
                    e => e.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "");

            return new GitAuth { Type = auth.Type, Credentials = creds.Count > 0 ? creds : null };
        }

        return new GitAuth { Type = auth.Type };
    }

    private static List<GitScript>? BuildScripts(List<PostmanEvent>? events)
    {
        if (events is not { Count: > 0 }) return null;

        var scripts = events
            .Where(e => e.Script?.Exec is { Count: > 0 })
            .Select(e => new GitScript
            {
                Type = e.Listen == "prerequest" ? "http:beforeRequest" : "afterResponse",
                Language = e.Script!.Type ?? "text/javascript",
                Code = string.Join("\n", e.Script.Exec!)
            })
            .ToList();

        return scripts.Count > 0 ? scripts : null;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static void WriteYaml<T>(DirectoryInfo dir, string filename, T obj)
    {
        dir.Create();
        File.WriteAllText(Path.Combine(dir.FullName, filename), Yaml.Serialize(obj));
    }

    /// <summary>Replaces characters that are invalid in Windows file/folder names.</summary>
    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }
}
