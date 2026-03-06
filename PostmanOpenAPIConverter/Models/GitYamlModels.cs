using YamlDotNet.Serialization;

namespace PostmanOpenAPIConverter.Models;

/// <summary>Maps to <c>$kind: collection</c> written to <c>definition.yaml</c>.</summary>
public class GitCollection
{
    [YamlMember(Alias = "$kind", Order = 0)]
    public string Kind => "collection";

    [YamlMember(Order = 1)]
    public required string Name { get; init; }

    [YamlMember(Order = 2)]
    public string? Description { get; init; }

    [YamlMember(Order = 3)]
    public long? Order { get; init; }

    [YamlMember(Order = 4)]
    public Dictionary<string, string>? Variables { get; init; }

    [YamlMember(Order = 5)]
    public List<GitScript>? Scripts { get; init; }
}

/// <summary>Maps to <c>$kind: http-request</c> written to <c>{Name}.request.yaml</c>.</summary>
public class GitHttpRequest
{
    [YamlMember(Alias = "$kind", Order = 0)]
    public string Kind => "http-request";

    [YamlMember(Order = 1)]
    public string? Description { get; init; }

    [YamlMember(Order = 2)]
    public required string Url { get; init; }

    [YamlMember(Order = 3)]
    public required string Method { get; init; }

    [YamlMember(Order = 4)]
    public Dictionary<string, string>? Headers { get; init; }

    [YamlMember(Order = 5)]
    public Dictionary<string, string>? QueryParams { get; init; }

    [YamlMember(Order = 6)]
    public Dictionary<string, string>? PathVariables { get; init; }

    [YamlMember(Order = 7)]
    public GitBody? Body { get; init; }

    [YamlMember(Order = 8)]
    public GitAuth? Auth { get; init; }

    [YamlMember(Order = 9)]
    public List<GitScript>? Scripts { get; init; }

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
    [YamlMember(Order = 0)]
    public required string Type { get; init; }

    [YamlMember(Order = 1)]
    public object? Content { get; init; }
}

/// <summary>Auth configuration block.</summary>
public class GitAuth
{
    [YamlMember(Order = 0)]
    public required string Type { get; init; }

    [YamlMember(Order = 1)]
    public Dictionary<string, string>? Credentials { get; init; }
}

/// <summary>Pre-request or post-response script.</summary>
public class GitScript
{
    /// <summary>"http:beforeRequest" or "afterResponse".</summary>
    [YamlMember(Order = 0)]
    public required string Type { get; init; }

    /// <summary>Script language, e.g. "text/javascript".</summary>
    [YamlMember(Order = 1)]
    public string? Language { get; init; }

    [YamlMember(Order = 2)]
    public string? Code { get; init; }
}

/// <summary>Workspace globals at <c>postman/globals/workspace.globals.yaml</c>.</summary>
public class GitGlobals
{
    [YamlMember(Order = 0)]
    public required string Name { get; init; }

    [YamlMember(Order = 1)]
    public List<object> Values { get; init; } = [];
}

/// <summary>Workspace binding at <c>.postman/resources.yaml</c>.</summary>
public class GitResources
{
    [YamlMember(Alias = "workspaceId", Order = 0)]
    public required string WorkspaceId { get; init; }

    [YamlMember(Alias = "localResources", Order = 1)]
    public bool LocalResources { get; init; }
}
