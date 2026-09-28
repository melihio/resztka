using Resztka.Api.Contracts;
using Resztka.Core.Catalog;
using Resztka.Core.Planning;

namespace Resztka.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Catalog");

        group.MapGet("/ingredients", (FoodCatalog catalog) =>
                catalog.Ingredients.Select(i => new IngredientResponse(
                    i.Id,
                    i.Name,
                    i.Unit,
                    NutritionResponse.From(i.Nutrition),
                    i.ShelfLifeDays,
                    i.Categories.Order().ToList())))
            .WithSummary("List ingredients with nutrition per 100 g/ml or per piece.");

        group.MapGet("/recipes", IResult (FoodCatalog catalog, string? mealType) =>
            {
                MealType? filter = null;
                if (mealType is not null)
                {
                    if (!Enum.TryParse<MealType>(mealType, ignoreCase: true, out var parsed))
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["mealType"] = [$"Use one of: {string.Join(", ", Enum.GetNames<MealType>().Select(n => n.ToLowerInvariant()))}."],
                        });
                    }

                    filter = parsed;
                }

                return Results.Ok(catalog.Recipes
                    .Where(r => filter is null || r.MealTypes.Contains(filter.Value))
                    .Select(r => new RecipeResponse(
                        r.Id,
                        r.Name,
                        r.MealTypes,
                        r.Ingredients,
                        NutritionResponse.From(catalog.NutritionPerServing(r)))));
            })
            .Produces<IEnumerable<RecipeResponse>>()
            .WithSummary("List recipes, optionally only those for one meal type (breakfast, main or drink).");

        group.MapGet("/products", (FoodCatalog catalog, string? ingredient) =>
                catalog.Products
                    .Where(p => ingredient is null || p.IngredientId == ingredient)
                    .Select(p => new ProductResponse(p.Id, p.Name, p.NameEn, p.IngredientId, p.PackSize, p.Price, p.Store)))
            .WithSummary("List the packs that can be bought, optionally only those for one ingredient.");

        group.MapGet("/categories", () => Enum.GetValues<FoodCategory>())
            .WithSummary("List the food categories that can be avoided.");

        group.MapGet("/diets", () =>
                Enum.GetValues<Diet>().ToDictionary(d => d, d => Diets.ExcludedCategories(d).Order().ToList()))
            .WithSummary("List the supported diets and the categories each one avoids.");

        return app;
    }
}
