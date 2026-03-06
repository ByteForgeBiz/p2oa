using System.CommandLine;
using PostmanOpenAPIConverter.Converters;

// ── Options ──────────────────────────────────────────────────────────────────

var inputOption = new Option<FileInfo>("--input", ["-i"])
{
    Description = "Path to the Postman collection JSON file",
    Required = true
};

var outputOption = new Option<FileInfo?>("--output", ["-o"])
{
    Description = "Output file path (omit to write to stdout)"
};

var openApiVersionOption = new Option<string>("--openapi-version", ["-v"])
{
    Description = "OpenAPI version to emit: 3.1 (default), 3.0, 3.2, or 2.0",
    DefaultValueFactory = _ => "3.1"
};

// ── Commands ──────────────────────────────────────────────────────────────────

var toOpenApiCommand = new Command("to-openapi", "Convert a Postman collection to an OpenAPI YAML specification");
toOpenApiCommand.Add(inputOption);
toOpenApiCommand.Add(outputOption);
toOpenApiCommand.Add(openApiVersionOption);

toOpenApiCommand.SetAction(async (ParseResult parseResult) =>
{
    var input = parseResult.GetRequiredValue(inputOption);
    var output = parseResult.GetValue(outputOption);
    var versionString = parseResult.GetValue(openApiVersionOption) ?? "3.1";

    var version = versionString switch
    {
        "3.0" => OpenApiVersion.OpenApi3_0,
        "3.2" => OpenApiVersion.OpenApi3_2,
        "2.0" => OpenApiVersion.OpenApi2_0,
        _     => OpenApiVersion.OpenApi3_1
    };

    try
    {
        var json = await File.ReadAllTextAsync(input.FullName);
        var yaml = PostmanToOpenApiConverter.Convert(json, version);

        if (output is not null)
            await File.WriteAllTextAsync(output.FullName, yaml);
        else
            Console.Write(yaml);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
});

// ── to-postman-git command ────────────────────────────────────────────────────

var gitOutputOption = new Option<DirectoryInfo>("--output", ["-o"])
{
    Description = "Output directory where the Postman GIT-compatible YAML tree will be written",
    Required = true
};

var toPostmanGitCommand = new Command("to-postman-git",
    "Convert a Postman collection JSON to a Postman GIT-compatible YAML directory structure");
toPostmanGitCommand.Add(inputOption);
toPostmanGitCommand.Add(gitOutputOption);

toPostmanGitCommand.SetAction(async (ParseResult parseResult) =>
{
    var input = parseResult.GetRequiredValue(inputOption);
    var output = parseResult.GetRequiredValue(gitOutputOption);

    try
    {
        var json = await File.ReadAllTextAsync(input.FullName);
        PostmanToGitYamlConverter.Convert(json, output);
        Console.WriteLine($"Written to: {output.FullName}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
});

// ── from-postman-git command ──────────────────────────────────────────────────

var fromGitInputOption = new Option<DirectoryInfo>("--input", ["-i"])
{
    Description = "Path to the Postman GIT directory (repo root or collection directory)",
    Required = true
};

var fromGitOutputOption = new Option<FileInfo?>("--output", ["-o"])
{
    Description = "Output file path (omit to write to stdout)"
};

var fromGitCollectionOption = new Option<string?>("--collection", ["-c"])
{
    Description = "Collection name to convert (required when the directory contains multiple collections)"
};

var fromPostmanGitCommand = new Command("from-postman-git",
    "Convert a Postman GIT-compatible YAML directory structure to a Postman collection JSON");
fromPostmanGitCommand.Add(fromGitInputOption);
fromPostmanGitCommand.Add(fromGitOutputOption);
fromPostmanGitCommand.Add(fromGitCollectionOption);

fromPostmanGitCommand.SetAction(async (ParseResult parseResult) =>
{
    var input          = parseResult.GetRequiredValue(fromGitInputOption);
    var output         = parseResult.GetValue(fromGitOutputOption);
    var collectionName = parseResult.GetValue(fromGitCollectionOption);

    try
    {
        var json = PostmanGitYamlToJsonConverter.Convert(input, collectionName);

        if (output is not null)
            await File.WriteAllTextAsync(output.FullName, json);
        else
            Console.Write(json);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
});

// ── Root ──────────────────────────────────────────────────────────────────────

var rootCommand = new RootCommand("Bidirectional converter between Postman collections and OpenAPI specifications");
rootCommand.Add(toOpenApiCommand);
rootCommand.Add(toPostmanGitCommand);
rootCommand.Add(fromPostmanGitCommand);

return await rootCommand.Parse(args).InvokeAsync();
