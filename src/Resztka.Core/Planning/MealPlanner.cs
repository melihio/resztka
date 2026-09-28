using System.Globalization;
using Google.OrTools.Sat;
using Resztka.Core.Catalog;

namespace Resztka.Core.Planning;

public sealed class MealPlanner(FoodCatalog catalog)
{
    private const int GroszePerZloty = 100;

    private const int WasteScale = 1000;

    private const int FullCoverage = 1000;

    private const int NutrientScale = 10;

    public MealPlan Plan(PlanRequest request)
    {
        Validate(request);

        var model = new CpModel();
        var slots = request.MealsPerDay;

        var assignments = new List<(Recipe Recipe, int Day, int Slot, BoolVar Var)>();
        for (var day = 0; day < request.Days; day++)
        {
            for (var slot = 0; slot < slots.Count; slot++)
            {
                var slotVars = new List<BoolVar>();
                foreach (var recipe in catalog.Recipes.Where(r => IsAllowed(r, slots[slot], day, request)))
                {
                    var x = model.NewBoolVar($"x_{recipe.Id}_d{day}_s{slot}");
                    assignments.Add((recipe, day, slot, x));
                    slotVars.Add(x);
                }

                if (slotVars.Count == 0)
                {
                    return MealPlan.Infeasible(
                        $"No {Name(slots[slot])} recipe is possible on day {day + 1}: every one uses an avoided " +
                        "ingredient or one that will have spoiled by then.");
                }

                model.Add(LinearExpr.Sum(slotVars) == 1);
            }
        }

        foreach (var mealType in slots.Distinct())
        {
            var needed = slots.Count(s => s == mealType) * request.Days;
            var allowed = assignments.Where(a => slots[a.Slot] == mealType).Select(a => a.Recipe).Distinct().Count();
            if (allowed * request.MaxRepeatsPerRecipe < needed)
            {
                return MealPlan.Infeasible(
                    $"Only {allowed} {Name(mealType)} recipe(s) are allowed, and at most {request.MaxRepeatsPerRecipe} " +
                    $"repeats each cannot fill {needed} {Name(mealType)} slots. Allow more repeats per recipe.");
            }
        }

        foreach (var group in assignments.GroupBy(a => a.Recipe.Id))
            model.Add(LinearExpr.Sum(group.Select(a => a.Var)) <= request.MaxRepeatsPerRecipe);

        if (request.MinKcalPerDay is { } minKcal)
        {
            foreach (var day in assignments.GroupBy(a => a.Day))
            {
                var kcal = LinearExpr.NewBuilder();
                foreach (var a in day)
                    kcal.AddTerm(a.Var, (long)Math.Round(catalog.NutritionPerServing(a.Recipe).Kcal));
                model.Add(kcal >= minKcal);
            }
        }

        var coverage = LinearExpr.NewBuilder();
        foreach (var day in assignments.GroupBy(a => a.Day))
        {
            foreach (var (nutrient, target) in request.Targets.All().Where(t => t.Target > 0))
            {
                var c = model.NewIntVar(0, FullCoverage, $"c_d{day.Key}_{nutrient}");

                var intake = LinearExpr.NewBuilder();
                foreach (var a in day)
                    intake.AddTerm(a.Var, Scale(catalog.NutritionPerServing(a.Recipe)[nutrient]) * FullCoverage);

                model.Add(c * Scale(target) <= intake);
                coverage.AddTerm(c, 1);
            }
        }

        var usedIngredients = assignments
            .SelectMany(a => a.Recipe.Ingredients.Keys)
            .ToHashSet();

        var packs = new Dictionary<Product, IntVar>();
        foreach (var product in catalog.Products.Where(p => usedIngredients.Contains(p.IngredientId)))
        {
            var maxNeed = MaxPossibleNeed(product.IngredientId, assignments, request);
            var maxPacks = (maxNeed + product.PackSize - 1) / product.PackSize;
            packs[product] = model.NewIntVar(0, maxPacks, $"n_{product.Id}");
        }

        var cost = LinearExpr.NewBuilder();
        foreach (var (product, n) in packs)
            cost.AddTerm(n, ToGrosze(product.Price));

        model.Add(cost <= ToGrosze(request.Budget));

        var objective = LinearExpr.NewBuilder();
        objective.AddTerm(cost, WasteScale);

        foreach (var ingredientId in usedIngredients)
        {
            var ingredient = catalog.GetIngredient(ingredientId);

            var need = LinearExpr.NewBuilder();
            foreach (var a in assignments.Where(a => a.Recipe.Ingredients.ContainsKey(ingredientId)))
                need.AddTerm(a.Var, a.Recipe.Ingredients[ingredientId] * request.People);

            var supply = LinearExpr.NewBuilder();
            supply.Add(request.Pantry.GetValueOrDefault(ingredientId));
            foreach (var (product, n) in packs.Where(p => p.Key.IngredientId == ingredientId))
                supply.AddTerm(n, product.PackSize);

            model.Add(need <= supply);

            if (SpoilsBeforeNextShop(ingredient, request) && UnitPrice(ingredientId) is { } unitPrice)
            {
                objective.AddTerm(supply, unitPrice);
                objective.AddTerm(need, -unitPrice);
            }
        }

        var provenOptimal = true;
        var timeLimit = request.Goal == PlanGoal.MaxNutrition ? request.TimeLimit / 2 : request.TimeLimit;

        if (request.Goal == PlanGoal.MaxNutrition)
        {
            model.Maximize(coverage);
            var nutritionSolver = CreateSolver(timeLimit);
            var nutritionStatus = nutritionSolver.Solve(model);
            if (nutritionStatus is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
                return MealPlan.Infeasible(NoCombination(nutritionStatus));

            provenOptimal = nutritionStatus == CpSolverStatus.Optimal;

            model.Add(coverage >= (long)Math.Round(nutritionSolver.ObjectiveValue));
            foreach (var a in assignments)
                model.AddHint(a.Var, nutritionSolver.Value(a.Var));
            foreach (var n in packs.Values)
                model.AddHint(n, nutritionSolver.Value(n));
        }

        model.Minimize(objective);

        var solver = CreateSolver(timeLimit);
        var status = solver.Solve(model);
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
            return MealPlan.Infeasible(NoCombination(status));

        provenOptimal &= status == CpSolverStatus.Optimal;

        var meals = assignments
            .Where(a => solver.BooleanValue(a.Var))
            .OrderBy(a => a.Day).ThenBy(a => a.Slot)
            .Select(a => new PlannedMeal(a.Day, a.Slot, slots[a.Slot], a.Recipe))
            .ToList();

        var shoppingList = packs
            .Select(p => new ShoppingItem(p.Key, (int)solver.Value(p.Value)))
            .Where(i => i.Packs > 0)
            .OrderBy(i => i.Product.Name, StringComparer.CurrentCulture)
            .ToList();

        return new MealPlan(
            provenOptimal ? PlanStatus.Optimal : PlanStatus.Feasible,
            meals,
            shoppingList,
            ComputeLeftovers(meals, shoppingList, request));
    }

    private static CpSolver CreateSolver(TimeSpan timeLimit) => new()
    {
        StringParameters = string.Create(
            CultureInfo.InvariantCulture,
            $"max_time_in_seconds:{timeLimit.TotalSeconds}"),
    };

    private static string NoCombination(CpSolverStatus status) => status == CpSolverStatus.Infeasible
        ? "No combination of recipes fits the budget together with the other constraints (repeats, minimum kcal)."
        : "The solver ran out of time before finding any plan. Try a longer time limit.";

    private static string Name(MealType mealType) => mealType.ToString().ToLowerInvariant();

    private static long Scale(double amount) => (long)Math.Round(amount * NutrientScale);

    private bool IsAllowed(Recipe recipe, MealType slot, int day, PlanRequest request)
    {
        if (!recipe.MealTypes.Contains(slot))
            return false;

        foreach (var ingredientId in recipe.Ingredients.Keys)
        {
            var ingredient = catalog.GetIngredient(ingredientId);

            if (request.ExcludedIngredients.Contains(ingredientId) ||
                ingredient.Categories.Overlaps(request.ExcludedCategories))
                return false;

            if (ingredient.ShelfLifeDays is { } shelfLife && day >= shelfLife)
                return false;
        }

        return true;
    }

    private static bool SpoilsBeforeNextShop(Ingredient ingredient, PlanRequest request) =>
        ingredient.ShelfLifeDays is { } shelfLife && shelfLife <= request.Days;

    private static int MaxPossibleNeed(
        string ingredientId,
        List<(Recipe Recipe, int Day, int Slot, BoolVar Var)> assignments,
        PlanRequest request) =>
        assignments
            .GroupBy(a => (a.Day, a.Slot))
            .Sum(slot => slot.Max(a => a.Recipe.Ingredients.GetValueOrDefault(ingredientId))) * request.People;

    private long? UnitPrice(string ingredientId)
    {
        var products = catalog.ProductsFor(ingredientId).ToList();
        if (products.Count == 0)
            return null;

        return products.Min(p => ToGrosze(p.Price) * WasteScale / p.PackSize);
    }

    private List<Leftover> ComputeLeftovers(
        List<PlannedMeal> meals,
        List<ShoppingItem> shoppingList,
        PlanRequest request)
    {
        var supply = new Dictionary<string, int>(request.Pantry);
        foreach (var item in shoppingList)
            supply[item.Product.IngredientId] = supply.GetValueOrDefault(item.Product.IngredientId) + item.Product.PackSize * item.Packs;

        foreach (var meal in meals)
        {
            foreach (var (ingredientId, amount) in meal.Recipe.Ingredients)
                supply[ingredientId] = supply.GetValueOrDefault(ingredientId) - amount * request.People;
        }

        return supply
            .Where(s => s.Value > 0)
            .Select(s => catalog.GetIngredient(s.Key))
            .Select(ingredient => new Leftover(ingredient, supply[ingredient.Id], SpoilsBeforeNextShop(ingredient, request)))
            .OrderByDescending(l => l.IsWaste)
            .ThenBy(l => l.Ingredient.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    private void Validate(PlanRequest request)
    {
        if (request.Budget <= 0)
            throw new ArgumentException("Budget must be positive.", nameof(request));
        if (request.Days <= 0)
            throw new ArgumentException("Days must be positive.", nameof(request));
        if (request.MealsPerDay.Count == 0)
            throw new ArgumentException("At least one meal per day is required.", nameof(request));
        if (request.People <= 0)
            throw new ArgumentException("People must be positive.", nameof(request));
        if (request.MaxRepeatsPerRecipe <= 0)
            throw new ArgumentException("MaxRepeatsPerRecipe must be positive.", nameof(request));

        foreach (var ingredientId in request.Pantry.Keys.Concat(request.ExcludedIngredients))
        {
            if (!catalog.Ingredients.Any(i => i.Id == ingredientId))
            {
                var known = string.Join(", ", catalog.Ingredients.Select(i => i.Id).Order(StringComparer.Ordinal));
                throw new ArgumentException($"Unknown ingredient '{ingredientId}'. Known ingredients: {known}.", nameof(request));
            }
        }
    }

    private static long ToGrosze(decimal zloty) => (long)Math.Round(zloty * GroszePerZloty);
}
