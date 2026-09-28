namespace Resztka.Core.Catalog;

public sealed class FoodCatalog
{
    private readonly Dictionary<string, Ingredient> _ingredients;

    private FoodCatalog(
        IReadOnlyList<Ingredient> ingredients,
        IReadOnlyList<Product> products,
        IReadOnlyList<Recipe> recipes)
    {
        Ingredients = ingredients;
        Products = products;
        Recipes = recipes;
        _ingredients = ingredients.ToDictionary(i => i.Id);
    }

    public IReadOnlyList<Ingredient> Ingredients { get; }

    public IReadOnlyList<Product> Products { get; }

    public IReadOnlyList<Recipe> Recipes { get; }

    public Ingredient GetIngredient(string id) => _ingredients[id];

    public Recipe? FindRecipe(string id) => Recipes.FirstOrDefault(r => r.Id == id);

    public IEnumerable<Product> ProductsFor(string ingredientId) =>
        Products.Where(p => p.IngredientId == ingredientId);

    public Nutrition NutritionPerServing(Recipe recipe) =>
        recipe.Ingredients.Aggregate(default(Nutrition), (sum, i) => sum + _ingredients[i.Key].NutritionOf(i.Value));

    public static FoodCatalog Create(
        IReadOnlyList<Ingredient> ingredients,
        IReadOnlyList<Product> products,
        IReadOnlyList<Recipe> recipes)
    {
        var errors = new List<string>();

        AddDuplicateErrors(errors, "ingredient", ingredients.Select(i => i.Id));
        AddDuplicateErrors(errors, "product", products.Select(p => p.Id));
        AddDuplicateErrors(errors, "recipe", recipes.Select(r => r.Id));

        var ingredientIds = ingredients.Select(i => i.Id).ToHashSet();

        foreach (var ingredient in ingredients)
        {
            foreach (var nutrient in Enum.GetValues<Nutrient>())
            {
                if (ingredient.Nutrition[nutrient] < 0)
                    errors.Add($"ingredient '{ingredient.Id}' has negative {nutrient.ToString().ToLowerInvariant()}");
            }

            if (ingredient.ShelfLifeDays is < 1)
                errors.Add($"ingredient '{ingredient.Id}' must have a shelf life of at least 1 day");
        }

        foreach (var product in products)
        {
            if (!ingredientIds.Contains(product.IngredientId))
                errors.Add($"product '{product.Id}' refers to unknown ingredient '{product.IngredientId}'");
            if (product.PackSize <= 0)
                errors.Add($"product '{product.Id}' must have a positive pack size");
            if (product.Price <= 0)
                errors.Add($"product '{product.Id}' must have a positive price");
        }

        foreach (var recipe in recipes)
        {
            if (recipe.MealTypes.Count == 0)
                errors.Add($"recipe '{recipe.Id}' has no meal types");
            if (recipe.Ingredients.Count == 0)
                errors.Add($"recipe '{recipe.Id}' has no ingredients");

            foreach (var (ingredientId, amount) in recipe.Ingredients)
            {
                if (!ingredientIds.Contains(ingredientId))
                    errors.Add($"recipe '{recipe.Id}' refers to unknown ingredient '{ingredientId}'");
                if (amount <= 0)
                    errors.Add($"recipe '{recipe.Id}' needs a positive amount of '{ingredientId}'");
            }
        }

        if (errors.Count > 0)
            throw new CatalogValidationException(errors);

        return new FoodCatalog(ingredients, products, recipes);
    }

    private static void AddDuplicateErrors(List<string> errors, string kind, IEnumerable<string> ids)
    {
        foreach (var id in ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add($"duplicate {kind} id '{id}'");
    }
}
