using System.Text.Json.Serialization;
using Awards.Api.Services;
using Awards.Api.Storage;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(options =>
    {
        // Report validation errors under the names clients send: sourceId, not SourceId.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());

        // Without this, unparsable JSON also reports a confusing "request field is required" error.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        // Keep serializer internals, such as .NET type names, out of error responses.
        options.AllowInputFormatterExceptionMessages = false;

        // The web defaults are too lenient for this API: they would accept "5" as a number,
        // SOURCEID as sourceId, and let the last of two repeated properties silently win.
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
        options.JsonSerializerOptions.AllowDuplicateProperties = false;
    });

builder.Services.AddProblemDetails();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<InMemoryStore>();
builder.Services.AddSingleton<PointsService>();

var app = builder.Build();

// Unhandled exceptions become ProblemDetails. Kestrel's own request errors, such as a body
// over the size limit, keep their real status instead of being reported as a 500.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});

// Empty error responses from outside the controllers, such as unknown routes, become ProblemDetails.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
