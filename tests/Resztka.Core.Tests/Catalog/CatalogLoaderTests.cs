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

    [Fact]
    public void LoadFromDirectory_BundledData_KcalMatchesMacros()
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);

        Assert.All(catalog.Ingredients, i =>
        {
            var difference = Math.Abs(i.Nutrition.Kcal - i.Nutrition.KcalFromMacros);
            Assert.True(difference <= Math.Max(5, i.Nutrition.Kcal * 0.1),
                $"{i.Id}: {i.Nutrition.Kcal} kcal on the label but {i.Nutrition.KcalFromMacros:F0} kcal from macros");
        });
    }

    [Fact]
    public void LoadFromDirectory_BundledData_ReadsCategories()
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);

        Assert.Equal([FoodCategory.Pork, FoodCategory.Beef], catalog.GetIngredient("minced-meat").Categories.Order());
        Assert.Empty(catalog.GetIngredient("rice").Categories);
    }

    [Fact]
    public void LoadFromDirectory_BundledData_UsesEveryCategory()
    {
        var catalog = CatalogLoader.LoadFromDirectory(RepositoryPaths.DataDirectory);

        var used = catalog.Ingredients.SelectMany(i => i.Categories).ToHashSet();

        Assert.All(Enum.GetValues<FoodCategory>(), c => Assert.Contains(c, used));
    }
}
