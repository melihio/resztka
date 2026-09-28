using System.Text.Json;
using System.Text.Json.Serialization;
using Resztka.Api.Endpoints;
using Resztka.Core.Catalog;
using Resztka.Core.Planning;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddSingleton(_ =>
{
    var directory = builder.Configuration["Catalog:Directory"]
        ?? Path.Combine(AppContext.BaseDirectory, "data");
    return CatalogLoader.LoadFromDirectory(directory);
});
builder.Services.AddSingleton<MealPlanner>();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");

app.MapCatalogEndpoints();
app.MapPlanEndpoints();

app.Run();

public partial class Program;
