using Resztka.Core.Catalog;

namespace Resztka.Core.Tests.Catalog;

public class FoodCatalogTests
{
    private static readonly Ingredient Rice = new("rice", "Rice", Unit.Gram, new Nutrition(350, 7, 0.6, 78, 1.3));
    private static readonly Ingredient Milk = new("milk", "Milk", Unit.Millilitre, new Nutrition(50, 3.4, 2, 4.8, 0), ShelfLifeDays: 5);
    private static readonly Ingredient Egg = new("egg", "Egg", Unit.Piece, new Nutrition(78, 6.3, 5.3, 0.6, 0), ShelfLifeDays: 21);

    [Fact]
    public void Create_ValidCatalog_ComputesNutritionPerServing()
    {
        var pudding = new Recipe("pudding", "Rice pudding", [MealType.Breakfast],
            new Dictionary<string, int> { ["rice"] = 60, ["milk"] = 400, ["egg"] = 1 });

        var catalog = FoodCatalog.Create(
            [Rice, Milk, Egg],
            [new Product("rice-400", "Rice 400 g", "rice", 400, 3.99m, "Test")],
            [pudding]);

        var nutrition = catalog.NutritionPerServing(pudding);
        Assert.Equal(350 * 0.6 + 50 * 4 + 78, nutrition.Kcal, precision: 6);
        Assert.Equal(7 * 0.6 + 3.4 * 4 + 6.3, nutrition.Protein, precision: 6);
        Assert.Single(catalog.ProductsFor("rice"));
        Assert.Empty(catalog.ProductsFor("milk"));
    }

    [Fact]
    public void Create_UnknownIngredientReference_Throws()
    {
        var recipe = new Recipe("pilaf", "Pilaf", [MealType.Main],
            new Dictionary<string, int> { ["rice"] = 100, ["chicken"] = 150 });

        var ex = Assert.Throws<CatalogValidationException>(() =>
            FoodCatalog.Create([Rice], [new Product("chicken-500", "Chicken", "chicken", 500, 14.99m, "Test")], [recipe]));

        Assert.Contains("product 'chicken-500' refers to unknown ingredient 'chicken'", ex.Errors);
        Assert.Contains("recipe 'pilaf' refers to unknown ingredient 'chicken'", ex.Errors);
    }

    [Fact]
    public void Create_NegativeNutrient_Throws()
    {
        var broken = Rice with { Nutrition = Rice.Nutrition with { Fat = -1 } };

        var ex = Assert.Throws<CatalogValidationException>(() => FoodCatalog.Create([broken], [], []));

        Assert.Contains("ingredient 'rice' has negative fat", ex.Errors);
    }

    [Fact]
    public void Create_DuplicateIdsAndBadValues_ReportsEveryError()
    {
        var ex = Assert.Throws<CatalogValidationException>(() => FoodCatalog.Create(
            [Rice, Rice],
            [new Product("rice-0", "Broken", "rice", 0, 0m, "Test")],
            []));

        Assert.Equal(3, ex.Errors.Count);
    }
}
