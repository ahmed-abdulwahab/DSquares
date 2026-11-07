using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Awards.Api.Tests;

/// <summary>
/// xUnit creates a new instance per test, so each test gets its own host and seeded singleton store.
/// </summary>
public abstract class ApiTestBase : IDisposable
{
    protected const long W100Seed = 100;
    protected const long W200Seed = 50;
    protected const string ValidationTitle = "One or more validation errors occurred.";

    private readonly WebApplicationFactory<Program> _factory = new();

    protected ApiTestBase()
    {
        Client = _factory.CreateClient();
    }

    protected HttpClient Client { get; }

    protected Task<HttpResponseMessage> PostAwardAsync(string sourceId, string externalTransactionId, string walletId, int points) =>
        Client.PostAsJsonAsync("/awards", new { sourceId, externalTransactionId, walletId, points });

    protected Task<HttpResponseMessage> PostRawAsync(string body, string contentType = "application/json") =>
        Client.PostAsync("/awards", new StringContent(body, Encoding.UTF8, contentType));

    protected async Task<long> GetBalanceAsync(string walletId)
    {
        var response = await Client.GetAsync($"/wallets/{walletId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonObjectAsync(response);
        return body.GetProperty("balance").GetInt64();
    }

    protected static async Task<JsonElement> ReadJsonObjectAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        return document.RootElement.Clone();
    }

    /// <summary>Asserts the response is RFC 9457 ProblemDetails with this status and title, and returns the body.</summary>
    protected static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement.Clone();
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("type").GetString()));
        return body;
    }

    /// <summary>
    /// Asserts a validation problem that names only this field. Failures from the validation attributes
    /// use the field name as the key, and JSON type mismatches use its JSON path, such as "$.points".
    /// </summary>
    protected static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        var body = await AssertProblemAsync(response, HttpStatusCode.BadRequest, ValidationTitle);
        var error = Assert.Single(body.GetProperty("errors").EnumerateObject());
        Assert.Contains(error.Name, new[] { field, $"$.{field}" });
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
