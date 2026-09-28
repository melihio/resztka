using System.Globalization;
using Resztka.Core.Catalog;
using Resztka.Core.Planning;

namespace Resztka.Cli;

internal static class PlanRenderer
{
    private const int Width = 64;

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static void Write(TextWriter output, FoodCatalog catalog, PlanRequest request, MealPlan plan)
    {
        var people = request.People == 1 ? "1 person" : $"{request.People} people";
        output.WriteLine($"resztka · {request.Days} days · {people} · budget {Money(request.Budget)}");
        output.WriteLine();

        if (plan.Status == PlanStatus.Infeasible)
        {
            output.WriteLine("No plan fits this budget. Try a higher budget, fewer meals, more");
            output.WriteLine("allowed repeats (--max-repeats) or a lower --min-kcal.");
            return;
        }

        foreach (var day in plan.Meals.GroupBy(m => m.Day))
        {
            var kcal = day.Sum(m => catalog.KcalPerServing(m.Recipe));
            output.WriteLine(Row($"Day {day.Key + 1}", $"{kcal:F0} kcal"));
            foreach (var meal in day)
                output.WriteLine($"  {Label(meal.MealType),-10} {meal.Recipe.Name}");
            output.WriteLine();
        }

        var stores = string.Join(", ", plan.ShoppingList.Select(i => i.Product.Store).Distinct());
        output.WriteLine($"Shopping list ({stores})");
        foreach (var item in plan.ShoppingList)
            output.WriteLine(Row($"  {item.Packs} × {item.Product.Name}", Money(item.Cost)));
        output.WriteLine(new string('─', Width));
        output.WriteLine(Row("  Total", Money(plan.TotalCost)));
        output.WriteLine(Row("  Left of budget", Money(request.Budget - plan.TotalCost)));

        var pantry = plan.Leftovers.Where(l => !l.IsWaste).ToList();
        var waste = plan.Leftovers.Where(l => l.IsWaste).ToList();

        if (pantry.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Stays in the pantry for next week");
            output.WriteLine("  " + string.Join(", ", pantry.Select(Amount)));
        }

        if (waste.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Will spoil before the next shop");
            output.WriteLine("  " + string.Join(", ", waste.Select(Amount)));
        }

        if (plan.Status == PlanStatus.Feasible)
        {
            output.WriteLine();
            output.WriteLine("Note: the time limit was reached, a cheaper plan may exist.");
        }
    }

    private static string Row(string left, string right) =>
        left.Length + right.Length + 1 >= Width
            ? $"{left} {right}"
            : left + right.PadLeft(Width - left.Length);

    private static string Money(decimal amount) => amount.ToString("0.00", Culture) + " zł";

    private static string Label(MealType mealType) => mealType switch
    {
        MealType.Breakfast => "Breakfast",
        MealType.Main => "Main",
        _ => mealType.ToString(),
    };

    private static string Amount(Leftover leftover)
    {
        var unit = leftover.Ingredient.Unit switch
        {
            Unit.Gram => " g",
            Unit.Millilitre => " ml",
            _ => " pcs",
        };

        return $"{leftover.Ingredient.Name.ToLowerInvariant()} {leftover.Amount}{unit}";
    }
}
