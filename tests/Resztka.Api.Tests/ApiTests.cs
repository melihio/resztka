using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Resztka.Api.Tests;

public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object SmallPlan(object? extra = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["budget"] = 60,
            ["days"] = 2,
            ["meals"] = new[] { "breakfast", "main" },
            ["timeLimitSeconds"] = 3,
        };

        if (extra is not null)
        {
            foreach (var property in extra.GetType().GetProperties())
                body[property.Name] = property.GetValue(extra);
        }

        return body;
    }

    private async Task<JsonElement> PostPlan(object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await _client.PostAsJsonAsync("/api/plans", body);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        Assert.Equal("Healthy", await _client.GetStringAsync("/health"));
    }

    [Fact]
    public async Task OpenApi_DocumentIsServed()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        Assert.True(document.GetProperty("paths").TryGetProperty("/api/plans", out _));
    }

    [Fact]
    public async Task Recipes_CanBeFilteredByMealType()
    {
        var drinks = await _client.GetFromJsonAsync<JsonElement>("/api/recipes?mealType=drink");

        Assert.NotEqual(0, drinks.GetArrayLength());
        Assert.All(drinks.EnumerateArray(), r =>
            Assert.Contains("drink", r.GetProperty("mealTypes").EnumerateArray().Select(t => t.GetString())));
    }

    [Fact]
    public async Task Recipes_UnknownMealType_IsBadRequest()
    {
        var response = await _client.GetAsync("/api/recipes?mealType=lunch");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Products_IncludeEnglishNames()
    {
        var products = await _client.GetFromJsonAsync<JsonElement>("/api/products?ingredient=coffee");

        var coffee = Assert.Single(products.EnumerateArray());
        Assert.Equal("Ground coffee 250 g", coffee.GetProperty("nameEn").GetString());
    }

    [Fact]
    public async Task Diets_ListTheirCategories()
    {
        var diets = await _client.GetFromJsonAsync<JsonElement>("/api/diets");

        Assert.Equal(["chicken", "pork", "beef"], diets.GetProperty("pescatarian").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task Plans_ReturnsAPlanWithinBudget()
    {
        var plan = await PostPlan(SmallPlan(new { drinks = new Dictionary<string, int> { ["tea"] = 2 } }));

        Assert.Contains(plan.GetProperty("status").GetString(), new[] { "optimal", "feasible" });
        Assert.True(plan.GetProperty("totalCost").GetDecimal() <= 60m);
        Assert.Equal(2, plan.GetProperty("days").GetArrayLength());
        Assert.Equal("tea", plan.GetProperty("drinks")[0].GetProperty("recipeId").GetString());
        Assert.Contains(plan.GetProperty("shoppingList").EnumerateArray(), i => i.GetProperty("productId").GetString() == "bdr-tea-100");
        Assert.True(plan.GetProperty("nutrition").GetProperty("averagePerDay").GetProperty("kcal").GetDouble() > 0);
    }

    [Fact]
    public async Task Plans_RespectsDietAndAvoidedCategories()
    {
        var plan = await PostPlan(SmallPlan(new { diet = "vegetarian", avoid = new[] { "sugar" } }));
        var ingredients = await _client.GetFromJsonAsync<JsonElement>("/api/ingredients");
        var recipes = await _client.GetFromJsonAsync<JsonElement>("/api/recipes");

        var forbidden = ingredients.EnumerateArray()
            .Where(i => i.GetProperty("categories").EnumerateArray()
                .Any(c => c.GetString() is "chicken" or "pork" or "beef" or "fish" or "sugar"))
            .Select(i => i.GetProperty("id").GetString())
            .ToHashSet();
        var recipeIngredients = recipes.EnumerateArray().ToDictionary(
            r => r.GetProperty("id").GetString()!,
            r => r.GetProperty("ingredients").EnumerateObject().Select(p => p.Name).ToList());

        var used = plan.GetProperty("days").EnumerateArray()
            .SelectMany(d => d.GetProperty("meals").EnumerateArray())
            .SelectMany(m => recipeIngredients[m.GetProperty("recipeId").GetString()!]);

        Assert.DoesNotContain(used, forbidden.Contains);
    }

    [Fact]
    public async Task Plans_WhenImpossible_ReturnsTheReason()
    {
        var plan = await PostPlan(SmallPlan(new { budget = 1 }));

        Assert.Equal("infeasible", plan.GetProperty("status").GetString());
        Assert.Contains("budget", plan.GetProperty("reason").GetString());
        Assert.Equal(0, plan.GetProperty("days").GetArrayLength());
    }

    [Fact]
    public async Task Plans_UnknownDrink_IsBadRequest()
    {
        var problem = await PostPlan(
            SmallPlan(new { drinks = new Dictionary<string, int> { ["beer"] = 1 } }),
            HttpStatusCode.BadRequest);

        Assert.StartsWith("Unknown drink 'beer'.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Plans_UnknownCategory_IsBadRequest()
    {
        await PostPlan(SmallPlan(new { avoid = new[] { "tofu" } }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Plans_TooLongTimeLimit_IsBadRequest()
    {
        await PostPlan(SmallPlan(new { timeLimitSeconds = 300 }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Plans_NegativeBudget_IsBadRequest()
    {
        var problem = await PostPlan(SmallPlan(new { budget = -5 }), HttpStatusCode.BadRequest);

        Assert.Equal("Budget must be positive.", problem.GetProperty("detail").GetString());
    }
}
