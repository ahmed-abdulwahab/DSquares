using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Awards.Api.Tests;

public sealed class SwaggerTests : ApiTestBase
{
    [Fact]
    public async Task Swagger_document_lists_both_endpoints_in_development()
    {
        var response = await Client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/awards", out _));
        Assert.True(paths.TryGetProperty("/wallets/{walletId}", out _));
    }

    [Fact]
    public async Task Swagger_document_uses_a_pattern_the_browser_can_run()
    {
        // Swagger UI runs patterns as JavaScript regexes, where \z would demand a literal "z"
        // and make it refuse valid wallet IDs before sending the request.
        var json = await Client.GetStringAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(json);

        var pattern = document.RootElement
            .GetProperty("paths").GetProperty("/wallets/{walletId}").GetProperty("get")
            .GetProperty("parameters")[0].GetProperty("schema").GetProperty("pattern").GetString();

        Assert.Equal("^[A-Za-z0-9_-]{1,64}$", pattern);
    }

    [Theory]
    [InlineData("/awards", "post", "201", "application/json")]
    [InlineData("/awards", "post", "200", "application/json")]
    [InlineData("/awards", "post", "400", "application/problem+json")]
    [InlineData("/awards", "post", "404", "application/problem+json")]
    [InlineData("/awards", "post", "409", "application/problem+json")]
    [InlineData("/awards", "post", "415", "application/problem+json")]
    [InlineData("/wallets/{walletId}", "get", "200", "application/json")]
    [InlineData("/wallets/{walletId}", "get", "400", "application/problem+json")]
    [InlineData("/wallets/{walletId}", "get", "404", "application/problem+json")]
    public async Task Swagger_document_declares_the_real_content_type(string path, string method, string status, string contentType)
    {
        var json = await Client.GetStringAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(json);

        var content = document.RootElement
            .GetProperty("paths").GetProperty(path).GetProperty(method)
            .GetProperty("responses").GetProperty(status).GetProperty("content");

        Assert.Equal(contentType, Assert.Single(content.EnumerateObject()).Name);
    }

    [Fact]
    public async Task Swagger_is_not_exposed_outside_development()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host => host.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
