using System.Text.Json;
using System.Text.Json.Serialization;

namespace Resztka.Core.Catalog;

public static class CatalogLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static FoodCatalog LoadFromDirectory(string directory)
    {
        var ingredients = Read<List<IngredientDto>>(Path.Combine(directory, "ingredients.json"))
            .Select(i => new Ingredient(i.Id, i.Name, i.Unit, i.KcalPerUnit, i.ShelfLifeDays))
            .ToList();

        var recipes = Read<List<RecipeDto>>(Path.Combine(directory, "recipes.json"))
            .Select(r => new Recipe(r.Id, r.Name, r.MealTypes, r.Ingredients))
            .ToList();

        var products = new List<Product>();
        var productsDirectory = Path.Combine(directory, "products");
        if (Directory.Exists(productsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(productsDirectory, "*.json").Order())
            {
                var store = Read<StoreDto>(file);
                products.AddRange(store.Products.Select(p =>
                    new Product(p.Id, p.Name, p.Ingredient, p.PackSize, p.Price, store.Store)));
            }
        }

        return FoodCatalog.Create(ingredients, products, recipes);
    }

    private static T Read<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, Options)
            ?? throw new InvalidDataException($"'{path}' is empty.");
    }

    private sealed record IngredientDto(string Id, string Name, Unit Unit, double KcalPerUnit, int? ShelfLifeDays);

    private sealed record RecipeDto(string Id, string Name, List<MealType> MealTypes, Dictionary<string, int> Ingredients);

    private sealed record StoreDto(string Store, string PricesAsOf, List<ProductDto> Products);

    private sealed record ProductDto(string Id, string Name, string Ingredient, int PackSize, decimal Price);
}
