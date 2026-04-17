using FluentAssertions;
using PostmanOpenAPIConverter.Converters;
using PostmanOpenAPIConverter.Models;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace PostmanOpenAPIConverter.Tests;

public class PostmanToGitYamlConverterTests
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
        .Build();

    [Fact]
    public void Convert_ValidJsonString_CreatesYamlFiles()
    {
        // Arrange
        var postmanJson = """
        {
            "info": {
                "name": "Test Collection",
                "description": "A test collection"
            },
            "item": [
                {
                    "name": "Get Users",
                    "request": {
                        "method": "GET",
                        "url": "https://api.example.com/users"
                    }
                }
            ]
        }
        """;
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(postmanJson, outputDir);

            // Assert
            File.Exists(Path.Combine(tempDir, ".postman", "resources.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "postman", "globals", "workspace.globals.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "postman", "collections", "Test_Collection", ".resources", "definition.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Get_Users.request.yaml")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_InvalidJsonString_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act & Assert
            Action act = () => PostmanToGitYamlConverter.Convert(invalidJson, outputDir);
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithVariables_CreatesVariablesYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Variable = [
                new PostmanVariable { Key = "baseUrl", Value = "https://api.example.com" },
                new PostmanVariable { Key = "version", Value = "v1" }
            ],
            Item = [
                new PostmanItem
                {
                    Name = "Get Users",
                    Request = new PostmanRequest
                    {
                        Method = "GET",
                        Url = new PostmanUrl { Raw = "{{baseUrl}}/{{version}}/users" }
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var definitionPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", ".resources", "definition.yaml");
            var yamlContent = File.ReadAllText(definitionPath);
            var gitCollection = YamlDeserializer.Deserialize<GitCollection>(yamlContent);

            gitCollection.Name.Should().Be("Test Collection");
            gitCollection.Variables.Should().NotBeNull();
            gitCollection.Variables["baseUrl"].Should().Be("https://api.example.com");
            gitCollection.Variables["version"].Should().Be("v1");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithHeaders_CreatesHeadersInRequestYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Get Users",
                    Request = new PostmanRequest
                    {
                        Method = "GET",
                        Url = new PostmanUrl { Raw = "https://api.example.com/users" },
                        Header = [
                            new PostmanHeader { Key = "Authorization", Value = "Bearer token" },
                            new PostmanHeader { Key = "Content-Type", Value = "application/json" }
                        ]
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var requestPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Get_Users.request.yaml");
            var yamlContent = File.ReadAllText(requestPath);
            var gitRequest = YamlDeserializer.Deserialize<GitHttpRequest>(yamlContent);

            gitRequest.Method.Should().Be("GET");
            gitRequest.Headers.Should().NotBeNull();
            gitRequest.Headers["Authorization"].Should().Be("Bearer token");
            gitRequest.Headers["Content-Type"].Should().Be("application/json");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithQueryParams_CreatesQueryParamsInRequestYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Get Users",
                    Request = new PostmanRequest
                    {
                        Method = "GET",
                        Url = new PostmanUrl
                        {
                            Raw = "https://api.example.com/users?page=1&limit=10",
                            Query = [
                                new PostmanQueryParam { Key = "page", Value = "1" },
                                new PostmanQueryParam { Key = "limit", Value = "10" }
                            ]
                        }
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var requestPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Get_Users.request.yaml");
            var yamlContent = File.ReadAllText(requestPath);
            var gitRequest = YamlDeserializer.Deserialize<GitHttpRequest>(yamlContent);

            gitRequest.Method.Should().Be("GET");
            gitRequest.QueryParams.Should().NotBeNull();
            gitRequest.QueryParams["page"].Should().Be("1");
            gitRequest.QueryParams["limit"].Should().Be("10");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithPathVariables_CreatesPathVariablesInRequestYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Get User",
                    Request = new PostmanRequest
                    {
                        Method = "GET",
                        Url = new PostmanUrl
                        {
                            Raw = "https://api.example.com/users/:id",
                            Variable = [
                                new PostmanVariable { Key = "id", Value = "123" }
                            ]
                        }
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var requestPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Get_User.request.yaml");
            var yamlContent = File.ReadAllText(requestPath);
            var gitRequest = YamlDeserializer.Deserialize<GitHttpRequest>(yamlContent);

            gitRequest.Method.Should().Be("GET");
            gitRequest.PathVariables.Should().NotBeNull();
            gitRequest.PathVariables["id"].Should().Be("123");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithRawJsonBody_CreatesBodyInRequestYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Create User",
                    Request = new PostmanRequest
                    {
                        Method = "POST",
                        Url = new PostmanUrl { Raw = "https://api.example.com/users" },
                        Body = new PostmanBody
                        {
                            Mode = "raw",
                            Raw = "{\"name\": \"John Doe\"}",
                            Options = new PostmanBodyOptions
                            {
                                Raw = new PostmanBodyRawOptions { Language = "json" }
                            }
                        }
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var requestPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Create_User.request.yaml");
            var yamlContent = File.ReadAllText(requestPath);
            var gitRequest = YamlDeserializer.Deserialize<GitHttpRequest>(yamlContent);

            gitRequest.Method.Should().Be("POST");
            gitRequest.Body.Should().NotBeNull();
            gitRequest.Body.Type.Should().Be("json");
            gitRequest.Body.Content.Should().Be("{\"name\": \"John Doe\"}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithUrlencodedBody_CreatesBodyInRequestYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Create User",
                    Request = new PostmanRequest
                    {
                        Method = "POST",
                        Url = new PostmanUrl { Raw = "https://api.example.com/users" },
                        Body = new PostmanBody
                        {
                            Mode = "urlencoded",
                            Urlencoded = [
                                new PostmanKeyValuePair { Key = "name", Value = "John Doe" },
                                new PostmanKeyValuePair { Key = "email", Value = "john@example.com" }
                            ]
                        }
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var requestPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Create_User.request.yaml");
            var yamlContent = File.ReadAllText(requestPath);
            var gitRequest = YamlDeserializer.Deserialize<GitHttpRequest>(yamlContent);

            gitRequest.Method.Should().Be("POST");
            gitRequest.Body.Should().NotBeNull();
            gitRequest.Body.Type.Should().Be("urlencoded");
            var content = ((Dictionary<object, object>)gitRequest.Body.Content!).ToDictionary(kv => (string)kv.Key, kv => (string)kv.Value);
            content["name"].Should().Be("John Doe");
            content["email"].Should().Be("john@example.com");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithFormdataBody_CreatesBodyInRequestYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Upload File",
                    Request = new PostmanRequest
                    {
                        Method = "POST",
                        Url = new PostmanUrl { Raw = "https://api.example.com/upload" },
                        Body = new PostmanBody
                        {
                            Mode = "formdata",
                            Formdata = [
                                new PostmanKeyValuePair { Key = "file", Value = "file content", Type = "file" },
                                new PostmanKeyValuePair { Key = "description", Value = "A file", Type = "text" }
                            ]
                        }
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var requestPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Upload_File.request.yaml");
            var yamlContent = File.ReadAllText(requestPath);
            var gitRequest = YamlDeserializer.Deserialize<GitHttpRequest>(yamlContent);

            gitRequest.Method.Should().Be("POST");
            gitRequest.Body.Should().NotBeNull();
            gitRequest.Body.Type.Should().Be("formdata");
            var content = ((List<object>)gitRequest.Body.Content!).Cast<Dictionary<object, object>>().Select(d => d.ToDictionary(kv => (string)kv.Key, kv => (string)kv.Value)).ToList();
            content.Count.Should().Be(2);
            content[0]["key"].Should().Be("file");
            content[0]["value"].Should().Be("file content");
            content[0]["type"].Should().Be("file");
            content[1]["key"].Should().Be("description");
            content[1]["value"].Should().Be("A file");
            content[1]["type"].Should().Be("text");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithAuth_CreatesAuthInRequestYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Get Users",
                    Request = new PostmanRequest
                    {
                        Method = "GET",
                        Url = new PostmanUrl { Raw = "https://api.example.com/users" },
                        Auth = new PostmanAuth
                        {
                            Type = "bearer",
                            Extra = new Dictionary<string, JsonElement>
                            {
                                ["bearer"] = JsonDocument.Parse("""
                                [
                                    {"key": "token", "value": "abc123"}
                                ]
                                """).RootElement
                            }
                        }
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var requestPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Get_Users.request.yaml");
            var yamlContent = File.ReadAllText(requestPath);
            var gitRequest = YamlDeserializer.Deserialize<GitHttpRequest>(yamlContent);

            gitRequest.Method.Should().Be("GET");
            gitRequest.Auth.Should().NotBeNull();
            gitRequest.Auth.Type.Should().Be("bearer");
            gitRequest.Auth.Credentials.Should().NotBeNull();
            gitRequest.Auth.Credentials["token"].Should().Be("abc123");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithScripts_CreatesScriptsInRequestYaml()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Get Users",
                    Request = new PostmanRequest
                    {
                        Method = "GET",
                        Url = new PostmanUrl { Raw = "https://api.example.com/users" }
                    },
                    Event = [
                        new PostmanEvent
                        {
                            Listen = "prerequest",
                            Script = new PostmanScript
                            {
                                Type = "text/javascript",
                                Exec = ["console.log('Pre-request script');"]
                            }
                        },
                        new PostmanEvent
                        {
                            Listen = "test",
                            Script = new PostmanScript
                            {
                                Type = "text/javascript",
                                Exec = ["pm.test('Status code is 200', function () { pm.response.to.have.status(200); });"]
                            }
                        }
                    ]
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            var requestPath = Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Get_Users.request.yaml");
            var yamlContent = File.ReadAllText(requestPath);
            var gitRequest = YamlDeserializer.Deserialize<GitHttpRequest>(yamlContent);

            gitRequest.Method.Should().Be("GET");
            gitRequest.Scripts.Should().NotBeNull();
            gitRequest.Scripts.Count.Should().Be(2);
            gitRequest.Scripts[0].Type.Should().Be("http:beforeRequest");
            gitRequest.Scripts[0].Language.Should().Be("text/javascript");
            gitRequest.Scripts[0].Code.Should().Be("console.log('Pre-request script');");
            gitRequest.Scripts[1].Type.Should().Be("afterResponse");
            gitRequest.Scripts[1].Language.Should().Be("text/javascript");
            gitRequest.Scripts[1].Code.Should().Contain("pm.test");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithFolders_CreatesFolderStructure()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Users Folder",
                    Item = [
                        new PostmanItem
                        {
                            Name = "Get Users",
                            Request = new PostmanRequest
                            {
                                Method = "GET",
                                Url = new PostmanUrl { Raw = "https://api.example.com/users" }
                            }
                        },
                        new PostmanItem
                        {
                            Name = "Create User",
                            Request = new PostmanRequest
                            {
                                Method = "POST",
                                Url = new PostmanUrl { Raw = "https://api.example.com/users" }
                            }
                        }
                    ]
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);

            // Assert
            Directory.Exists(Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Users_Folder")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Users_Folder", ".resources", "definition.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Users_Folder", "Get_Users.request.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "postman", "collections", "Test_Collection", "Users_Folder", "Create_User.request.yaml")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Convert_PostmanCollectionWithSpecialCharsInNames_SanitizesFileNames()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test: Collection?" },
            Item = [
                new PostmanItem
                {
                    Name = "Get <Users>",
                    Request = new PostmanRequest
                    {
                        Method = "GET",
                        Url = new PostmanUrl { Raw = "https://api.example.com/users" }
                    }
                }
            ]
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputDir = new DirectoryInfo(tempDir);

            // Act
            PostmanToGitYamlConverter.Convert(collection, outputDir);
            var folder = PostmanToGitYamlConverter.SanitizeName(collection.Info.Name);
            var file = PostmanToGitYamlConverter.SanitizeName(collection.Item[0].Name);

            // Assert
            Directory.Exists(Path.Combine(tempDir, "postman", "collections", folder)).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "postman", "collections", folder, file + ".request.yaml")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}