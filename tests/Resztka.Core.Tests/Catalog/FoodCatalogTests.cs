using Resztka.Core.Catalog;

namespace Resztka.Core.Tests.Catalog;

public class FoodCatalogTests
{
    private static readonly Ingredient Rice = new("rice", "Rice", Unit.Gram, 3.5);
    private static readonly Ingredient Milk = new("milk", "Milk", Unit.Millilitre, 0.5, ShelfLifeDays: 5);

    [Fact]
    public void Create_ValidCatalog_ComputesKcalPerServing()
    {
        var pudding = new Recipe("pudding", "Rice pudding", [MealType.Breakfast],
            new Dictionary<string, int> { ["rice"] = 60, ["milk"] = 400 });

        var catalog = FoodCatalog.Create(
            [Rice, Milk],
            [new Product("rice-400", "Rice 400 g", "rice", 400, 3.99m, "Test")],
            [pudding]);

        Assert.Equal(60 * 3.5 + 400 * 0.5, catalog.KcalPerServing(pudding), precision: 6);
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
    public void Create_DuplicateIdsAndBadValues_ReportsEveryError()
    {
        var ex = Assert.Throws<CatalogValidationException>(() => FoodCatalog.Create(
            [Rice, Rice],
            [new Product("rice-0", "Broken", "rice", 0, 0m, "Test")],
            []));

        Assert.Equal(3, ex.Errors.Count);
    }
}
