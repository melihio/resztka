using System.Globalization;
using Resztka.Core.Catalog;
using Resztka.Core.Planning;

namespace Resztka.Cli;

internal static class PlanRenderer
{
    private const int Width = 72;

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static void Write(TextWriter output, FoodCatalog catalog, PlanRequest request, MealPlan plan)
    {
        var people = request.People == 1 ? "1 person" : $"{request.People} people";
        var goal = request.Goal == PlanGoal.MaxNutrition ? "most nutrition" : "cheapest";
        output.WriteLine($"resztka · {request.Days} days · {people} · budget {Money(request.Budget)} · goal: {goal}");
        if (request.ExcludedCategories.Count > 0)
            output.WriteLine($"avoiding: {string.Join(", ", request.ExcludedCategories.Order().Select(c => c.ToString().ToLowerInvariant()))}");
        output.WriteLine();

        if (plan.Status == PlanStatus.Infeasible)
        {
            output.WriteLine("No plan is possible.");
            output.WriteLine(plan.Reason);
            output.WriteLine();
            output.WriteLine("Things to try: a higher --budget, fewer --meals or --days, a higher --max-repeats,");
            output.WriteLine("a lower --min-kcal, or fewer --avoid categories.");
            return;
        }

        var drinks = DrinksPerDay(catalog, plan);
        if (plan.Drinks.Count > 0)
        {
            output.WriteLine(Row(
                "Every day: " + string.Join(", ", plan.Drinks.Select(d => $"{d.Servings} × {d.Recipe.Name.ToLowerInvariant()}")),
                $"{drinks.Kcal:F0} kcal · {drinks.Protein:F0} g protein"));
            output.WriteLine();
        }

        foreach (var day in plan.Meals.GroupBy(m => m.Day))
        {
            var total = day.Aggregate(drinks, (sum, m) => sum + catalog.NutritionPerServing(m.Recipe));
            output.WriteLine(Row($"Day {day.Key + 1}", Summary(total)));
            foreach (var meal in day)
            {
                var nutrition = catalog.NutritionPerServing(meal.Recipe);
                output.WriteLine(Row(
                    $"  {Label(meal.MealType),-10} {meal.Recipe.Name}",
                    $"{nutrition.Kcal:F0} kcal · {nutrition.Protein:F0} g protein"));
            }

            output.WriteLine();
        }

        WriteNutrition(output, catalog, request, plan);
        output.WriteLine();

        var stores = string.Join(", ", plan.ShoppingList.Select(i => i.Product.Store).Distinct());
        output.WriteLine($"Shopping list ({stores})");
        foreach (var item in plan.ShoppingList)
        {
            output.WriteLine(Row($"  {item.Packs} × {item.Product.Name}", Money(item.Cost)));
            if (item.Product.NameEn is { } english)
                output.WriteLine($"      {english}");
        }
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
            output.WriteLine("Note: the time limit was reached before this plan could be proven the best one.");
            output.WriteLine("A slightly better plan may exist; --time-limit lets the solver search longer.");
        }
    }

    private static void WriteNutrition(TextWriter output, FoodCatalog catalog, PlanRequest request, MealPlan plan)
    {
        var days = plan.Meals.GroupBy(m => m.Day)
            .Select(d => d.Aggregate(DrinksPerDay(catalog, plan), (sum, m) => sum + catalog.NutritionPerServing(m.Recipe)))
            .ToList();
        var average = days.Aggregate(default(Nutrition), (sum, d) => sum + d) / days.Count;
        var targets = request.Targets.All().ToDictionary(t => t.Nutrient, t => t.Target);

        output.WriteLine("Nutrition per person, daily average");
        output.WriteLine($"  {"",-12}{"average",10}{"lowest day",12}{"target",10}{"",8}");
        foreach (var nutrient in Enum.GetValues<Nutrient>())
        {
            var unit = nutrient == Nutrient.Kcal ? "kcal" : "g";
            var lowest = days.Min(d => d[nutrient]);
            var line = $"  {NutrientName(nutrient),-12}{Quantity(average[nutrient], unit),10}{Quantity(lowest, unit),12}";

            if (targets.GetValueOrDefault(nutrient) is > 0 and var target)
                line += $"{Quantity(target, unit),10}{average[nutrient] / target,8:P0}";

            output.WriteLine(line);
        }
    }

    private static Nutrition DrinksPerDay(FoodCatalog catalog, MealPlan plan) =>
        plan.Drinks.Aggregate(default(Nutrition), (sum, d) => sum + catalog.NutritionPerServing(d.Recipe) * d.Servings);

    private static string Summary(Nutrition n) =>
        $"{n.Kcal:F0} kcal · {n.Protein:F0} g protein · {n.Fiber:F0} g fibre";

    private static string Quantity(double value, string unit) => $"{value.ToString("F0", Culture)} {unit}";

    private static string NutrientName(Nutrient nutrient) => nutrient switch
    {
        Nutrient.Kcal => "Energy",
        Nutrient.Protein => "Protein",
        Nutrient.Fat => "Fat",
        Nutrient.Carbs => "Carbs",
        Nutrient.Fiber => "Fibre",
        _ => nutrient.ToString(),
    };

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
