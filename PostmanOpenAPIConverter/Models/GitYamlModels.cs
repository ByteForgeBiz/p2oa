using YamlDotNet.Serialization;

namespace PostmanOpenAPIConverter.Models;

/// <summary>Maps to <c>$kind: collection</c> written to <c>definition.yaml</c>.</summary>
public class GitCollection
{
    /// <summary>
    /// Gets the kind identifier for this entity (always "collection").
    /// </summary>
    [YamlMember(Alias = "$kind", Order = 0)]
    public string Kind => "collection";

    /// <summary>
    /// Gets or initializes the name of the collection or folder.
    /// </summary>
    [YamlMember(Order = 1)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the description of the collection or folder.
    /// </summary>
    [YamlMember(Order = 2)]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes the display order of this item within its parent.
    /// </summary>
    [YamlMember(Order = 3)]
    public long? Order { get; init; }

    /// <summary>
    /// Gets or initializes the collection or folder-level variables as key-value pairs.
    /// </summary>
    [YamlMember(Order = 4)]
    public Dictionary<string, string>? Variables { get; init; }

    /// <summary>
    /// Gets or initializes the collection or folder-level scripts (pre-request or test).
    /// </summary>
    [YamlMember(Order = 5)]
    public List<GitScript>? Scripts { get; init; }
}

/// <summary>Maps to <c>$kind: http-request</c> written to <c>{Name}.request.yaml</c>.</summary>
public class GitHttpRequest
{
    /// <summary>
    /// Gets the kind identifier for this entity (always "http-request").
    /// </summary>
    [YamlMember(Alias = "$kind", Order = 0)]
    public string Kind => "http-request";

    /// <summary>
    /// Gets or initializes the description of the HTTP request.
    /// </summary>
    [YamlMember(Order = 1)]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes the URL for this HTTP request.
    /// </summary>
    [YamlMember(Order = 2)]
    public required string Url { get; init; }

    /// <summary>
    /// Gets or initializes the HTTP method (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    [YamlMember(Order = 3)]
    public required string Method { get; init; }

    /// <summary>
    /// Gets or initializes the HTTP headers as key-value pairs.
    /// </summary>
    [YamlMember(Order = 4)]
    public Dictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Gets or initializes the query parameters as key-value pairs.
    /// </summary>
    [YamlMember(Order = 5)]
    public Dictionary<string, string>? QueryParams { get; init; }

    /// <summary>
    /// Gets or initializes the path variables as key-value pairs.
    /// </summary>
    [YamlMember(Order = 6)]
    public Dictionary<string, string>? PathVariables { get; init; }

    /// <summary>
    /// Gets or initializes the request body.
    /// </summary>
    [YamlMember(Order = 7)]
    public GitBody? Body { get; init; }

    /// <summary>
    /// Gets or initializes the authentication configuration for this request.
    /// </summary>
    [YamlMember(Order = 8)]
    public GitAuth? Auth { get; init; }

    /// <summary>
    /// Gets or initializes the request-level scripts (pre-request or test).
    /// </summary>
    [YamlMember(Order = 9)]
    public List<GitScript>? Scripts { get; init; }

    /// <summary>
    /// Gets or initializes the display order of this request within its parent folder.
    /// </summary>
    [YamlMember(Order = 10)]
    public required long Order { get; init; }
}

/// <summary>
/// Request or response body. <see cref="Content"/> is polymorphic:
/// a raw string for <c>json</c>/<c>raw</c>/<c>xml</c>, a
/// <see cref="Dictionary{TKey,TValue}"/> for <c>urlencoded</c>, or a
/// list of key-value maps for <c>formdata</c>.
/// </summary>
public class GitBody
{
    /// <summary>
    /// Gets or initializes the body type ("json", "xml", "raw", "urlencoded", "formdata", etc.).
    /// </summary>
    [YamlMember(Order = 0)]
    public required string Type { get; init; }

    /// <summary>
    /// Gets or initializes the body content. The type varies based on <see cref="Type"/>:
    /// a string for json/xml/raw, a dictionary for urlencoded, or a list for formdata.
    /// </summary>
    [YamlMember(Order = 1)]
    public object? Content { get; init; }
}

/// <summary>Auth configuration block.</summary>
public class GitAuth
{
    /// <summary>
    /// Gets or initializes the authentication type ("bearer", "basic", "oauth2", etc.).
    /// </summary>
    [YamlMember(Order = 0)]
    public required string Type { get; init; }

    /// <summary>
    /// Gets or initializes the authentication credentials as key-value pairs.
    /// </summary>
    [YamlMember(Order = 1)]
    public Dictionary<string, string>? Credentials { get; init; }
}

/// <summary>Pre-request or post-response script.</summary>
public class GitScript
{
    /// <summary>"http:beforeRequest" or "afterResponse".</summary>
    [YamlMember(Order = 0)]
    public required string Type { get; init; }

    /// <summary>
    /// Gets or initializes the script language (e.g., "text/javascript").
    /// </summary>
    [YamlMember(Order = 1)]
    public string? Language { get; init; }

    /// <summary>
    /// Gets or initializes the script code content.
    /// </summary>
    [YamlMember(Order = 2)]
    public string? Code { get; init; }
}

/// <summary>Workspace globals at <c>postman/globals/workspace.globals.yaml</c>.</summary>
public class GitGlobals
{
    /// <summary>
    /// Gets or initializes the name of the globals workspace.
    /// </summary>
    [YamlMember(Order = 0)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the list of global variable values.
    /// </summary>
    [YamlMember(Order = 1)]
    public List<object> Values { get; init; } = [];
}

/// <summary>Workspace binding at <c>.postman/resources.yaml</c>.</summary>
public class GitResources
{
    /// <summary>
    /// Gets or initializes the workspace identifier (GUID).
    /// </summary>
    [YamlMember(Alias = "workspaceId", Order = 0)]
    public required string WorkspaceId { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether resources are stored locally.
    /// </summary>
    [YamlMember(Alias = "localResources", Order = 1)]
    public bool LocalResources { get; init; }
}
