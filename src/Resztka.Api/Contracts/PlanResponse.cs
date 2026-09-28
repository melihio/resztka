using Resztka.Core.Catalog;
using Resztka.Core.Planning;

namespace Resztka.Api.Contracts;

public sealed record PlanResponse(
    PlanStatus Status,
    string? Reason,
    decimal Budget,
    decimal TotalCost,
    decimal LeftOfBudget,
    IReadOnlyList<DayResponse> Days,
    IReadOnlyList<DrinkResponse> Drinks,
    NutritionSummaryResponse? Nutrition,
    IReadOnlyList<ShoppingItemResponse> ShoppingList,
    IReadOnlyList<LeftoverResponse> Leftovers)
{
    public static PlanResponse From(MealPlan plan, PlanRequest request, FoodCatalog catalog)
    {
        var drinksPerDay = plan.Drinks.Aggregate(
            default(Nutrition), (sum, d) => sum + catalog.NutritionPerServing(d.Recipe) * d.Servings);

        var days = plan.Meals
            .GroupBy(m => m.Day)
            .Select(day =>
            {
                var meals = day
                    .Select(m => new MealResponse(
                        m.MealType,
                        m.Recipe.Id,
                        m.Recipe.Name,
                        NutritionResponse.From(catalog.NutritionPerServing(m.Recipe))))
                    .ToList();
                var total = day.Aggregate(drinksPerDay, (sum, m) => sum + catalog.NutritionPerServing(m.Recipe));
                return (Response: new DayResponse(day.Key + 1, meals, NutritionResponse.From(total)), Total: total);
            })
            .ToList();

        NutritionSummaryResponse? summary = null;
        if (days.Count > 0)
        {
            var average = days.Aggregate(default(Nutrition), (sum, d) => sum + d.Total) / days.Count;
            var lowest = new Nutrition(
                days.Min(d => d.Total.Kcal),
                days.Min(d => d.Total.Protein),
                days.Min(d => d.Total.Fat),
                days.Min(d => d.Total.Carbs),
                days.Min(d => d.Total.Fiber));
            summary = new NutritionSummaryResponse(
                NutritionResponse.From(average),
                NutritionResponse.From(lowest),
                new TargetsRequest(request.Targets.Kcal, request.Targets.Protein, request.Targets.Fiber));
        }

        return new PlanResponse(
            plan.Status,
            plan.Reason,
            request.Budget,
            plan.TotalCost,
            plan.Status == PlanStatus.Infeasible ? request.Budget : request.Budget - plan.TotalCost,
            days.Select(d => d.Response).ToList(),
            plan.Drinks
                .Select(d => new DrinkResponse(
                    d.Recipe.Id,
                    d.Recipe.Name,
                    d.Servings,
                    NutritionResponse.From(catalog.NutritionPerServing(d.Recipe) * d.Servings)))
                .ToList(),
            summary,
            plan.ShoppingList
                .Select(i => new ShoppingItemResponse(
                    i.Product.Id,
                    i.Product.Name,
                    i.Product.NameEn,
                    i.Product.Store,
                    i.Packs,
                    i.Product.PackSize,
                    catalog.GetIngredient(i.Product.IngredientId).Unit,
                    i.Product.Price,
                    i.Cost))
                .ToList(),
            plan.Leftovers
                .Select(l => new LeftoverResponse(l.Ingredient.Id, l.Ingredient.Name, l.Amount, l.Ingredient.Unit, l.IsWaste))
                .ToList());
    }
}

public sealed record DayResponse(int Day, IReadOnlyList<MealResponse> Meals, NutritionResponse Nutrition);

public sealed record MealResponse(MealType MealType, string RecipeId, string Name, NutritionResponse Nutrition);

public sealed record DrinkResponse(string RecipeId, string Name, int ServingsPerDay, NutritionResponse NutritionPerDay);

public sealed record NutritionSummaryResponse(
    NutritionResponse AveragePerDay,
    NutritionResponse LowestDay,
    TargetsRequest Targets);

public sealed record ShoppingItemResponse(
    string ProductId,
    string Name,
    string? NameEn,
    string Store,
    int Packs,
    int PackSize,
    Unit Unit,
    decimal Price,
    decimal Cost);

public sealed record LeftoverResponse(string IngredientId, string Name, int Amount, Unit Unit, bool IsWaste);
