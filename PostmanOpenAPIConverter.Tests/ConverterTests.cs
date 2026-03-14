using Xunit;
using System.IO;
using PostmanOpenAPIConverter.Converters;

namespace PostmanOpenAPIConverter.Tests;

public class ConverterTests
{
    [Fact]
    public void PostmanToOpenApiConverter_ConvertsSampleCollection()
    {
        // Arrange
        var postmanJson = @"{
  ""info"": {
    ""name"": ""Test API"",
    ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
  },
  ""item"": [
    {
      ""name"": ""Get Users"",
      ""request"": {
        ""method"": ""GET"",
        ""header"": [],
        ""url"": {
          ""raw"": ""{{baseUrl}}/users"",
          ""host"": [""{{baseUrl}}""],
          ""path"": [""users""]
        }
      }
    }
  ],
  ""variable"": [
    {
      ""key"": ""baseUrl"",
      ""value"": ""https://api.example.com""
    }
  ]
}";

        // Act
        var openApiYaml = PostmanToOpenApiConverter.Convert(postmanJson);

        // Assert
        Assert.Contains("openapi: 3.0", openApiYaml);
        Assert.Contains("info:", openApiYaml);
        Assert.Contains("title: Test API", openApiYaml);
    }
}