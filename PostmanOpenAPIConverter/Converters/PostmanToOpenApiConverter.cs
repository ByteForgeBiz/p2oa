using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using PostmanOpenAPIConverter.Models;

namespace PostmanOpenAPIConverter.Converters;

/// <summary>
/// Converts Postman collections to OpenAPI (Swagger) YAML specifications.
/// Supports OpenAPI versions 2.0, 3.0, 3.1, and 3.2.
/// </summary>
public static class PostmanToOpenApiConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Converts a Postman collection JSON string to an OpenAPI YAML specification.
    /// </summary>
    /// <param name="postmanJson">The Postman collection JSON string.</param>
    /// <param name="openApiVersion">The target OpenAPI version (default is 3.0).</param>
    /// <returns>The OpenAPI specification in YAML format.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the JSON cannot be parsed.</exception>
    public static string Convert(string postmanJson, OpenApiVersion openApiVersion = OpenApiVersion.OpenApi3_0)
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

        return Convert(collection, openApiVersion);
    }

    /// <summary>
    /// Converts a Postman collection object to an OpenAPI YAML specification.
    /// </summary>
    /// <param name="collection">The Postman collection object.</param>
    /// <param name="openApiVersion">The target OpenAPI version (default is 3.1).</param>
    /// <returns>The OpenAPI specification in YAML format.</returns>
    public static string Convert(PostmanCollection collection, OpenApiVersion openApiVersion = OpenApiVersion.OpenApi3_1)
    {
        var document = BuildDocument(collection);
        return SerializeToYaml(document, openApiVersion);
    }

    // ── Document builder ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds an OpenAPI document from a Postman collection by extracting paths, operations, and server information.
    /// </summary>
    /// <param name="collection">The Postman collection to convert.</param>
    /// <returns>The constructed OpenAPI document.</returns>
    private static OpenApiDocument BuildDocument(PostmanCollection collection)
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = collection.Info.Name,
                Description = collection.Info.Description,
                Version = "1.0.0"
            },
            Paths = new OpenApiPaths()
        };

        string? serverUrl = null;
        var pendingTags = new List<(OpenApiOperation Op, string Tag)>();

        foreach (var (name, request, tag) in FlattenItems(collection.Item))
        {
            if (request.Url is null) continue;

            var (path, extractedServer) = ExtractPath(request.Url);
            serverUrl ??= extractedServer;

            if (!document.Paths.ContainsKey(path))
                document.Paths[path] = new OpenApiPathItem();

            var operation = BuildOperation(name, request);
            ((OpenApiPathItem)document.Paths[path]).AddOperation(ParseMethod(request.Method), operation);

            if (tag is not null)
                pendingTags.Add((operation, tag));
        }

        if (serverUrl is not null)
            document.Servers = [BuildServer(serverUrl)];

        // Tags require the document instance, so they're wired up in a second pass.
        if (pendingTags.Count > 0)
        {
            document.Tags = pendingTags.Select(t => t.Tag).Distinct()
                .Select(t => new OpenApiTag { Name = t })
                .ToHashSet();

            foreach (var (op, tag) in pendingTags)
                op.Tags = new HashSet<OpenApiTagReference> { new OpenApiTagReference(tag, document, null!) };
        }

        return document;
    }

    // ── Traversal ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recursively flattens the nested Postman item hierarchy into a sequence of requests with their folder tags.
    /// </summary>
    /// <param name="items">The collection of Postman items to flatten.</param>
    /// <param name="folderName">The parent folder name used as a tag (optional).</param>
    /// <returns>A sequence of tuples containing request name, request object, and folder tag.</returns>
    private static IEnumerable<(string Name, PostmanRequest Request, string? Tag)> FlattenItems(
        IEnumerable<PostmanItem> items, string? folderName = null)
    {
        foreach (var item in items)
        {
            if (item.Request is not null)
                yield return (item.Name, item.Request, folderName);
            else if (item.Item is not null)
                foreach (var nested in FlattenItems(item.Item, item.Name))
                    yield return nested;
        }
    }

    // ── Path extraction ──────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the OpenAPI path and server URL from a Postman URL object.
    /// </summary>
    /// <param name="url">The Postman URL to extract from.</param>
    /// <returns>A tuple containing the normalized path and optional server URL.</returns>
    private static (string Path, string? ServerUrl) ExtractPath(PostmanUrl url)
    {
        string? serverUrl = null;

        if (url.Host is { Count: > 0 })
            serverUrl = NormalizeServerUrl(string.Join(".", url.Host));

        IEnumerable<string> pathSegments;
        if (url.Path is { Count: > 0 })
        {
            pathSegments = url.Path.Select(NormalizePathSegment);
        }
        else
        {
            // Fall back to parsing the raw URL string
            (pathSegments, var fallbackServer) = ParseFromRaw(url.Raw);
            serverUrl ??= fallbackServer;
        }

        var parts = pathSegments.Where(s => !string.IsNullOrEmpty(s)).ToList();
        var path = parts.Count > 0 ? "/" + string.Join("/", parts) : "/";
        return (path, serverUrl);
    }

    /// <summary>
    /// Parses path segments and server URL from a raw URL string.
    /// </summary>
    /// <param name="raw">The raw URL string.</param>
    /// <returns>A tuple containing the path segments and optional server URL.</returns>
    private static (IEnumerable<string> Segments, string? ServerUrl) ParseFromRaw(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ([], null);

        var withoutProtocol = Regex.Replace(raw, @"^https?://", "");
        var qIdx = withoutProtocol.IndexOf('?');
        if (qIdx >= 0) withoutProtocol = withoutProtocol[..qIdx];

        var segments = withoutProtocol.Split('/');
        var serverUrl = segments.Length > 0 && !string.IsNullOrEmpty(segments[0])
            ? NormalizeServerUrl(segments[0])
            : null;

        var pathSegments = segments.Skip(1)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(NormalizePathSegment);

        return (pathSegments, serverUrl);
    }

    /// <summary>
    /// Converts Postman path-variable notations to OpenAPI <c>{param}</c> syntax:
    /// <list type="bullet">
    ///   <item><c>:param</c> → <c>{param}</c> (both v2.0 and v2.1)</item>
    ///   <item><c>{{param}}</c> → <c>{param}</c> (inline variable)</item>
    /// </list>
    /// </summary>
    private static string NormalizePathSegment(string segment)
    {
        if (segment.StartsWith(':'))
            return "{" + segment[1..] + "}";
        if (segment.StartsWith("{{") && segment.EndsWith("}}"))
            return "{" + segment[2..^2] + "}";
        return segment;
    }

    /// <summary>
    /// Turns a host string into a usable OpenAPI server URL.
    /// All <c>{{var}}</c> tokens are converted to <c>{var}</c> regardless of position,
    /// so both <c>{{baseUrl}}</c> and <c>sub.{{host}}.example.com</c> are handled.
    /// A plain host without a protocol gets an <c>https://</c> prefix.
    /// </summary>
    private static string NormalizeServerUrl(string host)
    {
        if (string.IsNullOrEmpty(host)) return host;
        var normalized = Regex.Replace(host, @"\{\{([^}]+)\}\}", "{$1}");
        // Add https:// only when the string is neither already a URL nor a bare variable like {baseUrl}
        return normalized.StartsWith("http") || normalized.StartsWith("{")
            ? normalized
            : "https://" + normalized;
    }

    /// <summary>
    /// Creates an OpenAPI server object from a server URL, extracting and declaring server variables.
    /// </summary>
    /// <param name="serverUrl">The server URL which may contain variable placeholders.</param>
    /// <returns>The configured OpenAPI server object.</returns>
    private static OpenApiServer BuildServer(string serverUrl)
    {
        // Collect every {varname} token in the URL and declare them as server variables.
        var varNames = Regex.Matches(serverUrl, @"\{([^}]+)\}")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        if (varNames.Count == 0)
            return new OpenApiServer { Url = serverUrl };

        return new OpenApiServer
        {
            Url = serverUrl,
            Variables = varNames.ToDictionary(v => v, _ => new OpenApiServerVariable { Default = "" })
        };
    }

    // ── Operation builder ────────────────────────────────────────────────────

    /// <summary>
    /// Builds an OpenAPI operation (endpoint) from a Postman request.
    /// </summary>
    /// <param name="name">The name/summary of the operation.</param>
    /// <param name="request">The Postman request object.</param>
    /// <returns>The constructed OpenAPI operation.</returns>
    private static OpenApiOperation BuildOperation(string name, PostmanRequest request)
    {
        var operation = new OpenApiOperation
        {
            Summary = name,
            Description = request.Description
        };

        var parameters = BuildParameters(request);
        if (parameters.Count > 0)
            operation.Parameters = parameters.Cast<IOpenApiParameter>().ToList();

        if (HasRequestBody(request.Method) && request.Body?.Raw is not null)
            operation.RequestBody = BuildRequestBody(request);

        operation.Responses = new OpenApiResponses
        {
            ["200"] = new OpenApiResponse { Description = "OK" }
        };

        return operation;
    }

    /// <summary>
    /// Extracts and builds OpenAPI parameters (path and query) from a Postman request.
    /// </summary>
    /// <param name="request">The Postman request containing URL parameters.</param>
    /// <returns>A list of OpenAPI parameter objects.</returns>
    private static List<OpenApiParameter> BuildParameters(PostmanRequest request)
    {
        var parameters = new List<OpenApiParameter>();

        // Path variables – prefer the explicit variable list (v2.1); fall back to
        // scanning path segments for {param} tokens (v2.0 / string-URL collections).
        var pathVars = request.Url?.Variable is { Count: > 0 }
            ? request.Url.Variable
                .Where(v => !string.IsNullOrEmpty(v.Key))
                .Select(v => (v.Key, v.Description))
                .ToList()
            : ExtractPathVarNames(request.Url)
                .Select(k => (Key: k, Description: (string?)null))
                .ToList();

        foreach (var (key, description) in pathVars)
        {
            parameters.Add(new OpenApiParameter
            {
                Name = key,
                In = ParameterLocation.Path,
                Required = true,
                Description = description,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }

        // Query parameters
        if (request.Url?.Query is { Count: > 0 })
        {
            foreach (var q in request.Url.Query.Where(q => q.Disabled != true && !string.IsNullOrEmpty(q.Key)))
            {
                parameters.Add(new OpenApiParameter
                {
                    Name = q.Key,
                    In = ParameterLocation.Query,
                    Description = q.Description,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }
        }

        return parameters;
    }

    /// <summary>
    /// Extracts path variable names from URL path segments (e.g., {id} from the path).
    /// </summary>
    /// <param name="url">The Postman URL object.</param>
    /// <returns>A sequence of path variable names.</returns>
    private static IEnumerable<string> ExtractPathVarNames(PostmanUrl? url)
    {
        if (url?.Path is null) yield break;
        foreach (var seg in url.Path)
        {
            if (seg.StartsWith('{') && seg.EndsWith('}'))
                yield return seg[1..^1];
        }
    }

    /// <summary>
    /// Determines whether the given HTTP method typically includes a request body.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>True if the method supports request bodies; otherwise, false.</returns>
    private static bool HasRequestBody(string method) =>
        method.ToUpperInvariant() is not ("GET" or "HEAD" or "OPTIONS" or "TRACE");

    /// <summary>
    /// Builds an OpenAPI request body object from a Postman request.
    /// </summary>
    /// <param name="request">The Postman request containing body data.</param>
    /// <returns>The constructed OpenAPI request body.</returns>
    private static OpenApiRequestBody BuildRequestBody(PostmanRequest request)
    {
        var contentType = ResolveContentType(request);
        return new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                [contentType] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
    }

    /// <summary>
    /// Determines the Content-Type for a Postman request body by checking headers, body options, or content sniffing.
    /// </summary>
    /// <param name="request">The Postman request.</param>
    /// <returns>The detected or inferred Content-Type string.</returns>
    private static string ResolveContentType(PostmanRequest request)
    {
        // 1. Explicit Content-Type header
        var header = request.Header?
            .FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)
                                 && h.Disabled != true);
        if (header is not null)
            return header.Value.Split(';')[0].Trim();

        // 2. Body language option (v2.1)
        if (request.Body?.Options?.Raw?.Language is { } lang)
        {
            return lang switch
            {
                "json" => "application/json",
                "xml"  => "application/xml",
                "html" => "text/html",
                _      => "text/plain"
            };
        }

        // 3. Sniff the raw body
        var raw = request.Body?.Raw?.TrimStart();
        if (raw is not null && (raw.StartsWith('{') || raw.StartsWith('[')))
            return "application/json";

        return "application/json";
    }

    /// <summary>
    /// Parses a string HTTP method into the corresponding <see cref="HttpMethod"/> enumeration value.
    /// </summary>
    /// <param name="method">The HTTP method string.</param>
    /// <returns>The corresponding HttpMethod value, defaulting to GET for unrecognized methods.</returns>
    private static HttpMethod ParseMethod(string method) =>
        method.ToUpperInvariant() switch
        {
            "GET"     => HttpMethod.Get,
            "POST"    => HttpMethod.Post,
            "PUT"     => HttpMethod.Put,
            "DELETE"  => HttpMethod.Delete,
            "PATCH"   => HttpMethod.Patch,
            "HEAD"    => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            "TRACE"   => HttpMethod.Trace,
            _         => HttpMethod.Get
        };

    // ── Serialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes an OpenAPI document to YAML format using the specified version.
    /// </summary>
    /// <param name="document">The OpenAPI document to serialize.</param>
    /// <param name="version">The target OpenAPI version.</param>
    /// <returns>The YAML string representation of the document.</returns>
    private static string SerializeToYaml(OpenApiDocument document, OpenApiVersion version)
    {
        var output = new StringWriter();
        var writer = new OpenApiYamlWriter(output);

        switch (version)
        {
            case OpenApiVersion.OpenApi2_0:
                document.SerializeAsV2(writer);
                break;
            case OpenApiVersion.OpenApi3_1:
                document.SerializeAsV31(writer);
                break;
            case OpenApiVersion.OpenApi3_2:
                document.SerializeAsV32(writer);
                break;
            default:
                document.SerializeAsV3(writer);
                break;
        }

        return output.ToString();
    }
}

/// <summary>Target OpenAPI specification version for serialization.</summary>
public enum OpenApiVersion
{
    OpenApi3_0,
    OpenApi3_1,
    OpenApi3_2,
    OpenApi2_0
}
