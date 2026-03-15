using FluentAssertions;
using PostmanOpenAPIConverter.Converters;
using PostmanOpenAPIConverter.Models;

namespace PostmanOpenAPIConverter.Tests;

public class PostmanToOpenApiConverterTests
{
    [Fact]
    public void Convert_ValidJsonString_ReturnsOpenApiYaml()
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

        // Act
        var result = PostmanToOpenApiConverter.Convert(postmanJson, OpenApiVersion.OpenApi31);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("openapi: '3.1.2'");
        result.Should().Contain("title: Test Collection");
        result.Should().Contain("description: A test collection");
        result.Should().Contain("/users:");
        result.Should().Contain("get:");
    }

    [Fact]
    public void Convert_InvalidJsonString_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Action act = () => PostmanToOpenApiConverter.Convert(invalidJson);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Convert_PostmanCollectionWithPathVariables_CreatesParameters()
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
                            Host = ["api", "example", "com"],
                            Path = ["users", ":id"],
                            Variable = [
                                new PostmanVariable { Key = "id", Value = "123", Description = "User ID" }
                            ]
                        }
                    }
                }
            ]
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi31);

        // Assert
        result.Should().Contain("parameters:");
        result.Should().Contain("name: id");
        result.Should().Contain("in: path");
        result.Should().Contain("required: true");
        result.Should().Contain("description: User ID");
    }

    [Fact]
    public void Convert_PostmanCollectionWithQueryParams_CreatesParameters()
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
                                new PostmanQueryParam { Key = "page", Value = "1", Description = "Page number" },
                                new PostmanQueryParam { Key = "limit", Value = "10", Description = "Items per page" }
                            ]
                        }
                    }
                }
            ]
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi31);

        // Assert
        result.Should().Contain("parameters:");
        result.Should().Contain("name: page");
        result.Should().Contain("in: query");
        result.Should().Contain("description: Page number");
        result.Should().Contain("name: limit");
        result.Should().Contain("description: Items per page");
    }

    [Fact]
    public void Convert_PostmanCollectionWithPostBody_CreatesRequestBody()
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
                            Raw = "{\"name\": \"John\"}",
                            Options = new PostmanBodyOptions
                            {
                                Raw = new PostmanBodyRawOptions { Language = "json" }
                            }
                        }
                    }
                }
            ]
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi31);

        // Assert
        result.Should().Contain("requestBody:");
        result.Should().Contain("application/json:");
    }

    [Fact]
    public void Convert_PostmanCollectionWithDifferentMethods_CreatesOperations()
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
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi31);

        // Assert
        result.Should().Contain("get:");
        result.Should().Contain("post:");
    }

    [Fact]
    public void Convert_PostmanCollectionWithFolders_FlattensItems()
    {
        // Arrange
        var collection = new PostmanCollection
        {
            Info = new PostmanInfo { Name = "Test Collection" },
            Item = [
                new PostmanItem
                {
                    Name = "Users",
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

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi31);

        // Assert
        result.Should().Contain("tags:");
        result.Should().Contain("name: Users");
        result.Should().Contain("get:");
        result.Should().Contain("post:");
    }

    [Fact]
    public void Convert_PostmanCollectionWithServer_CreatesServers()
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
                            Raw = "https://api.example.com/users",
                            Host = ["api", "example", "com"]
                        }
                    }
                }
            ]
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi31);

        // Assert
        result.Should().Contain("servers:");
        result.Should().Contain("url: https://api.example.com");
    }

    [Fact]
    public void Convert_PostmanCollectionWithVariablesInUrl_CreatesServerVariables()
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
                        Url = new PostmanUrl { Raw = "https://{{baseUrl}}/users" }
                    }
                }
            ]
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi31);

        // Assert
        result.Should().Contain("servers:");
        result.Should().Contain("url: 'https://{baseUrl}'");
        result.Should().Contain("variables:");
        result.Should().Contain("baseUrl:");
    }

    [Fact]
    public void Convert_PostmanCollection_OpenApi20Version_OutputsV2()
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
                    }
                }
            ]
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi20);

        // Assert
        result.Should().Contain("swagger: '2.0'");
    }

    [Fact]
    public void Convert_PostmanCollection_OpenApi30Version_OutputsV3()
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
                    }
                }
            ]
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi30);

        // Assert
        result.Should().Contain("openapi: 3.0.4");
    }

    [Fact]
    public void Convert_PostmanCollection_OpenApi32Version_OutputsV32()
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
                    }
                }
            ]
        };

        // Act
        var result = PostmanToOpenApiConverter.Convert(collection, OpenApiVersion.OpenApi32);

        // Assert
        result.Should().Contain("openapi: '3.2.0'");
    }
}