using Resztka.Core.Catalog;

namespace Resztka.Api.Contracts;

public sealed record NutritionResponse(double Kcal, double Protein, double Fat, double Carbs, double Fiber)
{
    public static NutritionResponse From(Nutrition n) => new(
        Math.Round(n.Kcal, 1),
        Math.Round(n.Protein, 1),
        Math.Round(n.Fat, 1),
        Math.Round(n.Carbs, 1),
        Math.Round(n.Fiber, 1));
}

public sealed record IngredientResponse(
    string Id,
    string Name,
    Unit Unit,
    NutritionResponse Nutrition,
    int? ShelfLifeDays,
    IReadOnlyList<FoodCategory> Categories);

public sealed record RecipeResponse(
    string Id,
    string Name,
    IReadOnlyList<MealType> MealTypes,
    IReadOnlyDictionary<string, int> Ingredients,
    NutritionResponse NutritionPerServing);

public sealed record ProductResponse(
    string Id,
    string Name,
    string? NameEn,
    string Ingredient,
    int PackSize,
    decimal Price,
    string Store);
