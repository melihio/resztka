using Resztka.Core.Catalog;

namespace Resztka.Core.Planning;

public enum PlanStatus
{
    Optimal,

    Feasible,

    Infeasible,
}

public sealed record PlannedMeal(int Day, int Slot, MealType MealType, Recipe Recipe);

public sealed record DailyDrink(Recipe Recipe, int Servings);

public sealed record ShoppingItem(Product Product, int Packs)
{
    public decimal Cost => Product.Price * Packs;
}

public sealed record Leftover(Ingredient Ingredient, int Amount, bool IsWaste);

public sealed record MealPlan(
    PlanStatus Status,
    IReadOnlyList<PlannedMeal> Meals,
    IReadOnlyList<ShoppingItem> ShoppingList,
    IReadOnlyList<Leftover> Leftovers)
{
    public decimal TotalCost => ShoppingList.Sum(i => i.Cost);

    public IReadOnlyList<DailyDrink> Drinks { get; init; } = [];

    public string? Reason { get; init; }

    public static MealPlan Infeasible(string reason) => new(PlanStatus.Infeasible, [], [], []) { Reason = reason };
}
