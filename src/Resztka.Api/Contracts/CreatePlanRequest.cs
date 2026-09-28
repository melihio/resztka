using Resztka.Core.Catalog;
using Resztka.Core.Planning;

namespace Resztka.Api.Contracts;

public sealed record CreatePlanRequest(
    decimal Budget,
    int? Days = null,
    IReadOnlyList<MealType>? Meals = null,
    int? People = null,
    PlanGoal? Goal = null,
    TargetsRequest? Targets = null,
    int? MinKcalPerDay = null,
    int? MaxRepeatsPerRecipe = null,
    IReadOnlyDictionary<string, int>? Drinks = null,
    IReadOnlyDictionary<string, int>? Pantry = null,
    IReadOnlyList<string>? ExcludeIngredients = null,
    IReadOnlyList<FoodCategory>? Avoid = null,
    Diet? Diet = null,
    double? TimeLimitSeconds = null);

public sealed record TargetsRequest(double Kcal, double Protein, double Fiber);
