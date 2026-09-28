using Resztka.Core.Catalog;

namespace Resztka.Core.Planning;

public enum Diet
{
    Vegetarian,

    Vegan,

    Pescatarian,
}

public static class Diets
{
    private static readonly FoodCategory[] Meat = [FoodCategory.Chicken, FoodCategory.Pork, FoodCategory.Beef];

    public static IReadOnlySet<FoodCategory> ExcludedCategories(Diet diet) => diet switch
    {
        Diet.Vegetarian => Meat.Append(FoodCategory.Fish).ToHashSet(),
        Diet.Vegan => Meat.Concat([FoodCategory.Fish, FoodCategory.Eggs, FoodCategory.Dairy]).ToHashSet(),
        Diet.Pescatarian => Meat.ToHashSet(),
        _ => throw new ArgumentOutOfRangeException(nameof(diet)),
    };
}
