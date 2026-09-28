using Resztka.Core.Catalog;
using Resztka.Core.Planning;

namespace Resztka.Core.Tests.Planning;

public class MealPlannerTests
{
    private static readonly Ingredient Rice = new("rice", "Rice", Unit.Gram, new Nutrition(350, 7, 0.6, 78, 1.3));
    private static readonly Ingredient Pasta = new("pasta", "Pasta", Unit.Gram, new Nutrition(360, 12.5, 1.5, 72, 3));
    private static readonly Ingredient Chicken = new("chicken", "Chicken", Unit.Gram, new Nutrition(110, 23, 1.5, 0, 0), ShelfLifeDays: 3);
    private static readonly Ingredient Milk = new("milk", "Milk", Unit.Millilitre, new Nutrition(50, 3.4, 2, 4.8, 0), ShelfLifeDays: 5);

    private static readonly Recipe ChickenPilaf = new("pilaf", "Chicken pilaf", [MealType.Main],
        new Dictionary<string, int> { ["rice"] = 100, ["chicken"] = 150 });

    private static readonly Recipe RicePudding = new("pudding", "Rice pudding", [MealType.Main],
        new Dictionary<string, int> { ["rice"] = 100, ["milk"] = 100 });

    private static readonly Recipe PlainPasta = new("pasta", "Plain pasta", [MealType.Main],
        new Dictionary<string, int> { ["pasta"] = 100 });

    private static readonly Recipe PlainRice = new("rice", "Plain rice", [MealType.Main],
        new Dictionary<string, int> { ["rice"] = 100 });

    private static FoodCatalog Catalog(params Product[] products) =>
        FoodCatalog.Create([Rice, Pasta, Chicken, Milk], products, [ChickenPilaf, RicePudding, PlainPasta, PlainRice]);

    private static FoodCatalog DefaultCatalog() =>
        Catalog(Pack("rice", 400, 4m), Pack("pasta", 500, 3m), Pack("chicken", 150, 10m), Pack("milk", 1000, 1m));

    private static Product Pack(string ingredientId, int size, decimal price) =>
        new($"{ingredientId}-{size}", $"{ingredientId} {size}", ingredientId, size, price, "Test");

    [Fact]
    public void Plan_OpenedPack_IsUsedUpByOtherRecipes()
    {
        var plan = new MealPlanner(DefaultCatalog()).Plan(new PlanRequest
        {
            Budget = 100m,
            Goal = PlanGoal.Cheapest,
            Days = 3,
            MealsPerDay = [MealType.Main],
            MaxRepeatsPerRecipe = 2,
        });

        Assert.Equal(PlanStatus.Optimal, plan.Status);
        Assert.Contains(plan.Meals, m => m.Recipe == RicePudding);
        Assert.DoesNotContain(plan.ShoppingList, i => i.Product.IngredientId == "pasta");
        Assert.Equal(1, plan.ShoppingList.Single(i => i.Product.IngredientId == "rice").Packs);
    }

    [Fact]
    public void Plan_ChoosesPackSizesThatMinimiseCost()
    {
        var catalog = FoodCatalog.Create([Rice], [Pack("rice", 400, 4m), Pack("rice", 1000, 6m)],
            [new Recipe("rice", "Rice", [MealType.Main], new Dictionary<string, int> { ["rice"] = 100 })]);

        var plan = new MealPlanner(catalog).Plan(new PlanRequest
        {
            Budget = 100m,
            Goal = PlanGoal.Cheapest,
            Days = 7,
            MealsPerDay = [MealType.Main],
            MaxRepeatsPerRecipe = 7,
        });

        var item = Assert.Single(plan.ShoppingList);
        Assert.Equal(1000, item.Product.PackSize);
        Assert.Equal(6m, plan.TotalCost);
    }

    [Fact]
    public void Plan_StaysWithinBudget()
    {
        var plan = new MealPlanner(DefaultCatalog()).Plan(new PlanRequest
        {
            Budget = 7m,
            Days = 3,
            MealsPerDay = [MealType.Main],
        });

        Assert.Equal(PlanStatus.Optimal, plan.Status);
        Assert.True(plan.TotalCost <= 7m);
    }

    [Fact]
    public void Plan_BudgetTooSmall_IsInfeasible()
    {
        var plan = new MealPlanner(DefaultCatalog()).Plan(new PlanRequest
        {
            Budget = 2m,
            Days = 3,
            MealsPerDay = [MealType.Main],
        });

        Assert.Equal(PlanStatus.Infeasible, plan.Status);
        Assert.Contains("budget", plan.Reason);
    }

    [Fact]
    public void Plan_NoAllowedRecipeForASlot_ExplainsWhy()
    {
        var catalog = FoodCatalog.Create([Rice, Chicken], [Pack("rice", 400, 4m), Pack("chicken", 150, 1m)], [ChickenPilaf]);

        var plan = new MealPlanner(catalog).Plan(new PlanRequest
        {
            Budget = 100m,
            Days = 4,
            MealsPerDay = [MealType.Main],
            MaxRepeatsPerRecipe = 4,
        });

        Assert.Equal(PlanStatus.Infeasible, plan.Status);
        Assert.Equal(
            "No main recipe is possible on day 4: every one uses an avoided ingredient or one that will have spoiled by then.",
            plan.Reason);
    }

    [Fact]
    public void Plan_TooFewRecipesForTheRepeatLimit_ExplainsWhy()
    {
        var plan = new MealPlanner(DefaultCatalog()).Plan(new PlanRequest
        {
            Budget = 100m,
            Days = 7,
            MealsPerDay = [MealType.Main],
            MaxRepeatsPerRecipe = 1,
        });

        Assert.Equal(PlanStatus.Infeasible, plan.Status);
        Assert.StartsWith("Only 4 main recipe(s) are allowed", plan.Reason);
    }

    [Fact]
    public void Plan_PerishableIngredients_AreEatenWithinShelfLife()
    {
        var catalog = FoodCatalog.Create([Rice, Pasta, Chicken],
            [Pack("rice", 400, 4m), Pack("pasta", 500, 3m), Pack("chicken", 300, 1m)],
            [ChickenPilaf, PlainPasta]);

        var plan = new MealPlanner(catalog).Plan(new PlanRequest
        {
            Budget = 100m,
            Days = 5,
            MealsPerDay = [MealType.Main],
            MaxRepeatsPerRecipe = 5,
        });

        Assert.Equal(PlanStatus.Optimal, plan.Status);
        Assert.All(plan.Meals.Where(m => m.Day >= 3), m => Assert.Equal(PlainPasta, m.Recipe));
    }

    [Fact]
    public void Plan_PantryIngredients_AreFree()
    {
        var plan = new MealPlanner(DefaultCatalog()).Plan(new PlanRequest
        {
            Budget = 100m,
            Goal = PlanGoal.Cheapest,
            Days = 2,
            MealsPerDay = [MealType.Main],
            Pantry = new Dictionary<string, int> { ["pasta"] = 200 },
        });

        Assert.All(plan.Meals, m => Assert.Equal(PlainPasta, m.Recipe));
        Assert.Empty(plan.ShoppingList);
        Assert.Equal(0m, plan.TotalCost);
    }

    [Fact]
    public void Plan_ExcludedIngredients_NeverAppear()
    {
        var plan = new MealPlanner(DefaultCatalog()).Plan(new PlanRequest
        {
            Budget = 100m,
            Days = 2,
            MealsPerDay = [MealType.Main],
            ExcludedIngredients = new HashSet<string> { "pasta" },
        });

        Assert.Equal(PlanStatus.Optimal, plan.Status);
        Assert.DoesNotContain(plan.Meals, m => m.Recipe.Ingredients.ContainsKey("pasta"));
    }

    [Fact]
    public void Plan_LeftoversThatSpoilBeforeNextShop_AreWaste()
    {
        var catalog = FoodCatalog.Create([Rice, Milk], [Pack("rice", 400, 4m), Pack("milk", 1000, 3m)], [RicePudding]);

        var plan = new MealPlanner(catalog).Plan(new PlanRequest
        {
            Budget = 100m,
            Days = 5,
            MealsPerDay = [MealType.Main],
            MaxRepeatsPerRecipe = 5,
        });

        Assert.Contains(plan.Leftovers, l => l.Ingredient == Milk && l.Amount == 500 && l.IsWaste);
        Assert.Contains(plan.Leftovers, l => l.Ingredient == Rice && l.Amount == 300 && !l.IsWaste);
    }

    [Fact]
    public void Plan_PerishablesThatOutlastThePlan_AreNotWaste()
    {
        var plan = new MealPlanner(FoodCatalog.Create([Rice, Milk], [Pack("rice", 400, 4m), Pack("milk", 1000, 3m)], [RicePudding]))
            .Plan(new PlanRequest { Budget = 100m, Days = 1, MealsPerDay = [MealType.Main] });

        Assert.Contains(plan.Leftovers, l => l.Ingredient == Milk && l.Amount == 900 && !l.IsWaste);
    }

    [Fact]
    public void Plan_BundledCatalog_PlansAWeekFor100Zloty()
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);

        var plan = new MealPlanner(catalog).Plan(new PlanRequest
        {
            Budget = 100m,
            Days = 7,
            MealsPerDay = [MealType.Breakfast, MealType.Main, MealType.Main],
            MinKcalPerDay = 1800,
        });

        Assert.NotEqual(PlanStatus.Infeasible, plan.Status);
        Assert.Equal(21, plan.Meals.Count);
        Assert.True(plan.TotalCost <= 100m);
    }

    private static readonly Recipe ChickenPasta = new("chicken-pasta", "Chicken pasta", [MealType.Main],
        new Dictionary<string, int> { ["pasta"] = 100, ["chicken"] = 200 });

    private static FoodCatalog PastaCatalog() => FoodCatalog.Create(
        [Pasta, Chicken],
        [Pack("pasta", 100, 3m), Pack("chicken", 200, 10m)],
        [PlainPasta, ChickenPasta]);

    private static PlanRequest OneMeal(decimal budget, PlanGoal goal, double proteinTarget) => new()
    {
        Budget = budget,
        Days = 1,
        MealsPerDay = [MealType.Main],
        Goal = goal,
        Targets = new NutritionTargets(Kcal: 0, Protein: proteinTarget, Fiber: 0),
    };

    [Fact]
    public void Plan_MaxNutrition_SpendsTheBudgetOnMoreNutritiousFood()
    {
        var planner = new MealPlanner(PastaCatalog());

        var cheapest = planner.Plan(OneMeal(100m, PlanGoal.Cheapest, 60));
        var nutritious = planner.Plan(OneMeal(100m, PlanGoal.MaxNutrition, 60));

        Assert.Equal(PlainPasta, Assert.Single(cheapest.Meals).Recipe);
        Assert.Equal(ChickenPasta, Assert.Single(nutritious.Meals).Recipe);
        Assert.Equal(13m, nutritious.TotalCost);
    }

    [Fact]
    public void Plan_MaxNutrition_StaysWithinBudget()
    {
        var plan = new MealPlanner(PastaCatalog()).Plan(OneMeal(5m, PlanGoal.MaxNutrition, 60));

        Assert.Equal(PlainPasta, Assert.Single(plan.Meals).Recipe);
        Assert.Equal(3m, plan.TotalCost);
    }

    [Fact]
    public void Plan_MaxNutrition_DoesNotPayForMoreThanTheTarget()
    {
        var plan = new MealPlanner(PastaCatalog()).Plan(OneMeal(100m, PlanGoal.MaxNutrition, 10));

        Assert.Equal(PlainPasta, Assert.Single(plan.Meals).Recipe);
        Assert.Equal(3m, plan.TotalCost);
    }

    [Fact]
    public void Plan_BundledCatalog_MaxNutritionBeatsCheapestOnProtein()
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);
        var planner = new MealPlanner(catalog);
        var request = new PlanRequest
        {
            Budget = 100m,
            Days = 7,
            MealsPerDay = [MealType.Breakfast, MealType.Main, MealType.Main],
        };

        var cheapest = planner.Plan(request with { Goal = PlanGoal.Cheapest });
        var nutritious = planner.Plan(request with { Goal = PlanGoal.MaxNutrition });

        double Protein(MealPlan plan) => plan.Meals.Sum(m => catalog.NutritionPerServing(m.Recipe).Protein);

        Assert.True(nutritious.TotalCost <= 100m);
        Assert.True(Protein(nutritious) > Protein(cheapest),
            $"protein: nutritious {Protein(nutritious):F0} g vs cheapest {Protein(cheapest):F0} g");
    }

    [Fact]
    public void Plan_ExcludedCategories_NeverAppear()
    {
        var chicken = Chicken with { Categories = new HashSet<FoodCategory> { FoodCategory.Chicken } };
        var catalog = FoodCatalog.Create(
            [Rice, Pasta, chicken],
            [Pack("rice", 400, 4m), Pack("pasta", 500, 3m), Pack("chicken", 150, 1m)],
            [ChickenPilaf, PlainPasta, PlainRice]);

        var plan = new MealPlanner(catalog).Plan(new PlanRequest
        {
            Budget = 100m,
            Days = 3,
            MealsPerDay = [MealType.Main],
            ExcludedCategories = new HashSet<FoodCategory> { FoodCategory.Chicken },
        });

        Assert.Equal(PlanStatus.Optimal, plan.Status);
        Assert.DoesNotContain(plan.Meals, m => m.Recipe == ChickenPilaf);
    }

    [Theory]
    [InlineData(Diet.Vegetarian)]
    [InlineData(Diet.Pescatarian)]
    public void Plan_BundledCatalog_SupportsDiets(Diet diet)
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);
        var excluded = Diets.ExcludedCategories(diet);

        var plan = new MealPlanner(catalog).Plan(new PlanRequest
        {
            Budget = 100m,
            Days = 7,
            ExcludedCategories = excluded,
            TimeLimit = TimeSpan.FromSeconds(5),
        });

        Assert.NotEqual(PlanStatus.Infeasible, plan.Status);
        Assert.All(plan.Meals.SelectMany(m => m.Recipe.Ingredients.Keys),
            id => Assert.False(catalog.GetIngredient(id).Categories.Overlaps(excluded), $"{id} is not allowed in a {diet} diet"));
    }

    [Fact]
    public void Diets_Vegan_ExcludesAllAnimalProducts()
    {
        var excluded = Diets.ExcludedCategories(Diet.Vegan);

        Assert.Equal(
            [FoodCategory.Chicken, FoodCategory.Pork, FoodCategory.Beef, FoodCategory.Fish, FoodCategory.Eggs, FoodCategory.Dairy],
            excluded.Order());
    }
}
