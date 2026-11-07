using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Awards.Api.Tests;

/// <summary>Errors raised outside the controller actions must also be ProblemDetails.</summary>
public sealed class ErrorHandlingTests : ApiTestBase
{
    private const string ValidBody = """{"sourceId":"S1","externalTransactionId":"T1","walletId":"W100","points":25}""";

    [Fact]
    public async Task Unmatched_route_returns_404_problem()
    {
        var response = await Client.GetAsync("/nothing-here");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Not Found");
    }

    [Fact]
    public async Task Unsupported_method_returns_405_problem()
    {
        var response = await Client.GetAsync("/awards");

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    public async Task Unsupported_content_type_returns_415_problem_and_changes_nothing(string contentType)
    {
        var response = await PostRawAsync(ValidBody, contentType);

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "Unsupported Media Type");
        Assert.Equal(W100Seed, await GetBalanceAsync("W100"));
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_problem_without_its_details()
    {
        const string Secret = "internal failure details";
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureServices(services =>
                services.Configure<MvcOptions>(options => options.Filters.Add(new ThrowingFilter(Secret)))));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/wallets/W100");

        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "An error occurred while processing your request.");
        Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private sealed class ThrowingFilter(string message) : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context) => throw new InvalidOperationException(message);

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
