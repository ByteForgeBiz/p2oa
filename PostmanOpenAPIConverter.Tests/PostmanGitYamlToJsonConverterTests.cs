using PostmanOpenAPIConverter.Converters;
using System.Text.Json.Nodes;
using Yaml = System.Collections.Generic.Dictionary<object, object>;

namespace PostmanOpenAPIConverter.Tests;

public class PostmanGitYamlToJsonConverterTests
{
    [Fact]
    public void Convert_ValidCollectionDirectory_ReturnsJsonString()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var resourcesDir = Path.Combine(tempDir, ".resources");
        Directory.CreateDirectory(resourcesDir);
        File.WriteAllText(Path.Combine(resourcesDir, "definition.yaml"), "name: Test Collection\ndescription: A test collection\n");

        try
        {
            var inputDir = new DirectoryInfo(tempDir);

            // Act
            var result = PostmanGitYamlToJsonConverter.Convert(inputDir);

            // Assert
            Assert.NotNull(result);
            var json = JsonNode.Parse(result) as JsonObject;
            Assert.NotNull(json);
            Assert.Equal("Test Collection", json["info"]?["name"]?.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindCollectionDir_InputIsCollectionDir_ReturnsInputDir()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var resourcesDir = Path.Combine(tempDir, ".resources");
        Directory.CreateDirectory(resourcesDir);
        File.WriteAllText(Path.Combine(resourcesDir, "definition.yaml"), "name: Test\n");

        try
        {
            var inputDir = new DirectoryInfo(tempDir);

            // Act
            var result = PostmanGitYamlToJsonConverter.FindCollectionDir(inputDir, null);

            // Assert
            Assert.Equal(inputDir.FullName, result.FullName);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindCollectionDir_PostmanCollectionsDir_ReturnsCollectionDir()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var collectionsDir = Path.Combine(tempDir, "postman", "collections");
        Directory.CreateDirectory(collectionsDir);
        var collectionDir = Path.Combine(collectionsDir, "TestCollection");
        Directory.CreateDirectory(collectionDir);
        var resourcesDir = Path.Combine(collectionDir, ".resources");
        Directory.CreateDirectory(resourcesDir);
        File.WriteAllText(Path.Combine(resourcesDir, "definition.yaml"), "name: Test\n");

        try
        {
            var inputDir = new DirectoryInfo(tempDir);

            // Act
            var result = PostmanGitYamlToJsonConverter.FindCollectionDir(inputDir, null);

            // Assert
            Assert.Equal(collectionDir, result.FullName);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindCollectionDir_NoCollection_ThrowsException()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputDir = new DirectoryInfo(tempDir);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => PostmanGitYamlToJsonConverter.FindCollectionDir(inputDir, null));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ConvertCollection_ValidDirectory_ReturnsJsonString()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var resourcesDir = Path.Combine(tempDir, ".resources");
        Directory.CreateDirectory(resourcesDir);
        File.WriteAllText(Path.Combine(resourcesDir, "definition.yaml"), "name: Test Collection\ndescription: A test collection\n");

        try
        {
            var collectionDir = new DirectoryInfo(tempDir);

            // Act
            var result = PostmanGitYamlToJsonConverter.ConvertCollection(collectionDir);

            // Assert
            Assert.NotNull(result);
            var json = JsonNode.Parse(result) as JsonObject;
            Assert.NotNull(json);
            Assert.Equal("Test Collection", json["info"]?["name"]?.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReadItems_DirectoryWithRequestFile_ReturnsItemsArray()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "Get Campaign.request.yaml"), "method: GET\nurl: https://api.example.com/campaigns\norder: 1\n");

        try
        {
            var dir = new DirectoryInfo(tempDir);

            // Act
            var result = PostmanGitYamlToJsonConverter.ReadItems(dir);

            // Assert
            Assert.Single(result);
            var item = result[0] as JsonObject;
            Assert.NotNull(item);
            Assert.Equal("Get Campaign", item["name"]?.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildRequestItem_ValidYaml_ReturnsRequestItem()
    {
        // Arrange
        var yaml = new Yaml
        {
            ["method"] = "POST",
            ["url"] = "https://api.example.com",
            ["description"] = "Test request"
        };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildRequestItem("Test Request", yaml);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Request", result["name"]?.ToString());
        var request = result["request"] as JsonObject;
        Assert.NotNull(request);
        Assert.Equal("POST", request["method"]?.ToString());
    }

    [Fact]
    public void BuildFolderItem_ValidYaml_ReturnsFolderItem()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var subDir = new DirectoryInfo(tempDir);
            var folderDef = new Yaml { ["name"] = "Test Folder", ["description"] = "A test folder" };

            // Act
            var result = PostmanGitYamlToJsonConverter.BuildFolderItem(subDir, folderDef);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Folder", result["name"]?.ToString());
            Assert.Equal("A test folder", result["description"]?.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildUrlJson_ValidYaml_ReturnsUrlObject()
    {
        // Arrange
        var yaml = new Yaml
        {
            ["url"] = "https://api.example.com",
            ["queryParams"] = new Yaml { ["key1"] = "value1" },
            ["pathVariables"] = new Yaml { ["id"] = "123" }
        };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildUrlJson(yaml);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://api.example.com", result["raw"]?.ToString());
        var query = result["query"] as JsonArray;
        Assert.NotNull(query);
        Assert.Single(query);
    }

    [Fact]
    public void BuildHeadersJson_ValidYaml_ReturnsHeadersArray()
    {
        // Arrange
        var yaml = new Yaml
        {
            ["headers"] = new Yaml { ["Content-Type"] = "application/json" }
        };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildHeadersJson(yaml);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var header = result[0] as JsonObject;
        Assert.NotNull(header);
        Assert.Equal("Content-Type", header["key"]?.ToString());
    }

    [Fact]
    public void BuildBodyJson_RawBody_ReturnsBodyObject()
    {
        // Arrange
        var yaml = new Yaml
        {
            ["body"] = new Yaml { ["type"] = "json", ["content"] = "{\"key\": \"value\"}" }
        };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildBodyJson(yaml);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("raw", result["mode"]?.ToString());
    }

    [Fact]
    public void BuildRawBody_ValidInput_ReturnsBodyObject()
    {
        // Act
        var result = PostmanGitYamlToJsonConverter.BuildRawBody("json", "{\"test\": true}");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("raw", result["mode"]?.ToString());
        Assert.Equal("{\"test\": true}", result["raw"]?.ToString());
    }

    [Fact]
    public void BuildUrlencodedBody_ValidContent_ReturnsBodyObject()
    {
        // Arrange
        var content = new Yaml { ["key1"] = "value1" };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildUrlencodedBody(content);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("urlencoded", result["mode"]?.ToString());
        var urlencoded = result["urlencoded"] as JsonArray;
        Assert.NotNull(urlencoded);
        Assert.Single(urlencoded);
    }

    [Fact]
    public void BuildFormdataBody_ValidContent_ReturnsBodyObject()
    {
        // Arrange
        var content = new List<object> { new Yaml { ["key"] = "file", ["value"] = "content", ["type"] = "file" } };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildFormdataBody(content);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("formdata", result["mode"]?.ToString());
        var formdata = result["formdata"] as JsonArray;
        Assert.NotNull(formdata);
        Assert.Single(formdata);
    }

    [Fact]
    public void BuildAuthJson_ValidYaml_ReturnsAuthObject()
    {
        // Arrange
        var yaml = new Yaml
        {
            ["auth"] = new Yaml
            {
                ["type"] = "basic",
                ["credentials"] = new Yaml { ["username"] = "user", ["password"] = "pass" }
            }
        };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildAuthJson(yaml);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("basic", result["type"]?.ToString());
        var basic = result["basic"] as JsonArray;
        Assert.NotNull(basic);
    }

    [Fact]
    public void BuildScriptsJson_ValidYaml_ReturnsEventsArray()
    {
        // Arrange
        var yaml = new Yaml
        {
            ["scripts"] = new List<object>
            {
                new Yaml { ["type"] = "test", ["language"] = "javascript", ["code"] = "console.log('test');" }
            }
        };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildScriptsJson(yaml);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var @event = result[0] as JsonObject;
        Assert.NotNull(@event);
        Assert.Equal("test", @event["listen"]?.ToString());
    }

    [Fact]
    public void BuildVariablesJson_ValidYaml_ReturnsVariablesArray()
    {
        // Arrange
        var yaml = new Yaml
        {
            ["variables"] = new Yaml { ["baseUrl"] = "https://api.example.com" }
        };

        // Act
        var result = PostmanGitYamlToJsonConverter.BuildVariablesJson(yaml);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var variable = result[0] as JsonObject;
        Assert.NotNull(variable);
        Assert.Equal("baseUrl", variable["key"]?.ToString());
    }

    [Fact]
    public void ReadYaml_ValidFile_ReturnsYamlDictionary()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "key: value\n");

        try
        {
            // Act
            var result = PostmanGitYamlToJsonConverter.ReadYaml(tempFile);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("key"));
            Assert.Equal("value", result["key"]?.ToString());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Str_ExistingKey_ReturnsValue()
    {
        // Arrange
        var dict = new Yaml { ["key"] = "value" };

        // Act
        var result = PostmanGitYamlToJsonConverter.Str(dict, "key");

        // Assert
        Assert.Equal("value", result);
    }

    [Fact]
    public void Str_NonExistingKey_ReturnsNull()
    {
        // Arrange
        var dict = new Yaml();

        // Act
        var result = PostmanGitYamlToJsonConverter.Str(dict, "key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Long_ValidLong_ReturnsValue()
    {
        // Arrange
        var dict = new Yaml { ["key"] = "123" };

        // Act
        var result = PostmanGitYamlToJsonConverter.Long(dict, "key");

        // Assert
        Assert.Equal(123L, result);
    }

    [Fact]
    public void Long_InvalidLong_ReturnsZero()
    {
        // Arrange
        var dict = new Yaml { ["key"] = "notanumber" };

        // Act
        var result = PostmanGitYamlToJsonConverter.Long(dict, "key");

        // Assert
        Assert.Equal(0L, result);
    }

    [Fact]
    public void RemoveNulls_ObjectWithNulls_RemovesNulls()
    {
        // Arrange
        var obj = new JsonObject
        {
            ["key1"] = "value1",
            ["key2"] = null,
            ["key3"] = "value3"
        };

        // Act
        PostmanGitYamlToJsonConverter.RemoveNulls(obj);

        // Assert
        Assert.Equal(2, obj.Count);
        Assert.True(obj.ContainsKey("key1"));
        Assert.True(obj.ContainsKey("key3"));
        Assert.False(obj.ContainsKey("key2"));
    }
}