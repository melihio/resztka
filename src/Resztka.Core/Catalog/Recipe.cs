namespace Resztka.Core.Catalog;

public sealed record Recipe(
    string Id,
    string Name,
    IReadOnlyList<MealType> MealTypes,
    IReadOnlyDictionary<string, int> Ingredients);
