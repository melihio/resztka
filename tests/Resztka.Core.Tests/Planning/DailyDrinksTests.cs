using Resztka.Core.Catalog;
using Resztka.Core.Planning;

namespace Resztka.Core.Tests.Planning;

public class DailyDrinksTests
{
    private static readonly Ingredient Pasta = new("pasta", "Pasta", Unit.Gram, new Nutrition(360, 12.5, 1.5, 72, 3));
    private static readonly Ingredient Tea = new("tea", "Tea bags", Unit.Piece, default);
    private static readonly Ingredient Juice = new("juice", "Orange juice", Unit.Millilitre, new Nutrition(45, 0.7, 0.2, 10, 0.2))
    {
        Categories = new HashSet<FoodCategory> { FoodCategory.Fruit },
    };
    private static readonly Ingredient Milk = new("milk", "Milk", Unit.Millilitre, new Nutrition(50, 3.4, 2, 4.8, 0), ShelfLifeDays: 5);

    private static readonly Recipe PlainPasta = new("pasta", "Plain pasta", [MealType.Main],
        new Dictionary<string, int> { ["pasta"] = 100 });

    private static readonly Recipe TeaDrink = new("tea", "Tea", [MealType.Drink],
        new Dictionary<string, int> { ["tea"] = 1 });

    private static readonly Recipe JuiceDrink = new("juice", "Glass of juice", [MealType.Drink],
        new Dictionary<string, int> { ["juice"] = 250 });

    private static readonly Recipe MilkDrink = new("milk", "Glass of milk", [MealType.Drink],
        new Dictionary<string, int> { ["milk"] = 250 });

    private static readonly FoodCatalog Catalog = FoodCatalog.Create(
        [Pasta, Tea, Juice, Milk],
        [
            new Product("pasta-700", "Pasta 700 g", "pasta", 700, 3m, "Test"),
            new Product("tea-100", "Tea 100 bags", "tea", 100, 8m, "Test"),
            new Product("juice-1000", "Juice 1 l", "juice", 1000, 5m, "Test"),
            new Product("milk-1000", "Milk 1 l", "milk", 1000, 3m, "Test"),
        ],
        [PlainPasta, TeaDrink, JuiceDrink, MilkDrink]);

    private static PlanRequest Week(decimal budget, params (string Id, int Servings)[] drinks) => new()
    {
        Budget = budget,
        Days = 7,
        MealsPerDay = [MealType.Main],
        MaxRepeatsPerRecipe = 7,
        Goal = PlanGoal.Cheapest,
        DailyDrinks = drinks.ToDictionary(d => d.Id, d => d.Servings),
    };

    [Fact]
    public void Plan_Drinks_AreBoughtAndCountTowardsTheBudget()
    {
        var plan = new MealPlanner(Catalog).Plan(Week(20m, ("tea", 2), ("juice", 1)));

        Assert.Equal(PlanStatus.Infeasible, plan.Status);

        plan = new MealPlanner(Catalog).Plan(Week(21m, ("tea", 2), ("juice", 1)));

        Assert.Equal(PlanStatus.Optimal, plan.Status);
        Assert.Equal(21m, plan.TotalCost);
        Assert.Equal(1, plan.ShoppingList.Single(i => i.Product.IngredientId == "tea").Packs);
        Assert.Equal(2, plan.ShoppingList.Single(i => i.Product.IngredientId == "juice").Packs);
        Assert.Equal(2, plan.Drinks.Count);
    }

    [Fact]
    public void Plan_Drinks_LeaveTheRestOfThePackInThePantry()
    {
        var plan = new MealPlanner(Catalog).Plan(Week(100m, ("tea", 2)));

        Assert.Contains(plan.Leftovers, l => l.Ingredient == Tea && l.Amount == 86 && !l.IsWaste);
    }

    [Fact]
    public void Plan_Drinks_CountTowardsTheMinimumKcal()
    {
        var withoutJuice = new MealPlanner(Catalog).Plan(Week(100m) with { MinKcalPerDay = 450 });
        var withJuice = new MealPlanner(Catalog).Plan(Week(100m, ("juice", 1)) with { MinKcalPerDay = 450 });

        Assert.Equal(PlanStatus.Infeasible, withoutJuice.Status);
        Assert.Equal(PlanStatus.Optimal, withJuice.Status);
    }

    [Fact]
    public void Plan_DrinkThatSpoilsBeforeTheEndOfThePlan_ExplainsWhy()
    {
        var plan = new MealPlanner(Catalog).Plan(Week(100m, ("milk", 1)));

        Assert.Equal(PlanStatus.Infeasible, plan.Status);
        Assert.Equal("Glass of milk is had every day, but milk only keeps 5 days after the shop. Plan at most 5 days.", plan.Reason);
    }

    [Fact]
    public void Plan_UnknownDrink_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new MealPlanner(Catalog).Plan(Week(100m, ("pasta", 1))));

        Assert.StartsWith("Unknown drink 'pasta'. Known drinks: tea, juice, milk.", ex.Message);
    }

    [Fact]
    public void Plan_DrinkWithAvoidedCategory_Throws()
    {
        var request = Week(100m, ("juice", 1)) with
        {
            ExcludedCategories = new HashSet<FoodCategory> { FoodCategory.Fruit },
        };

        var ex = Assert.Throws<ArgumentException>(() => new MealPlanner(Catalog).Plan(request));

        Assert.StartsWith("Drink 'juice' contains fruit, which is avoided.", ex.Message);
    }
}
