using Resztka.Core.Catalog;

namespace Resztka.Core.Tests.Catalog;

public class CatalogLoaderTests
{
    [Fact]
    public void LoadFromDirectory_BundledData_IsValid()
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);

        Assert.NotEmpty(catalog.Recipes);
        Assert.All(catalog.Products, p => Assert.Equal("Biedronka", p.Store));
    }

    [Fact]
    public void LoadFromDirectory_BundledData_EveryRecipeIngredientCanBeBought()
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);

        var missing = catalog.Recipes
            .SelectMany(r => r.Ingredients.Keys)
            .Distinct()
            .Where(id => !catalog.ProductsFor(id).Any());

        Assert.Empty(missing);
    }

    [Fact]
    public void LoadFromDirectory_BundledData_CoversEveryMealType()
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);

        foreach (var mealType in Enum.GetValues<MealType>())
            Assert.Contains(catalog.Recipes, r => r.MealTypes.Contains(mealType));
    }
}
