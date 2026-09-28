namespace Resztka.Core.Catalog;

public sealed record Ingredient(
    string Id,
    string Name,
    Unit Unit,
    Nutrition Nutrition,
    int? ShelfLifeDays = null)
{
    public bool IsPerishable => ShelfLifeDays is not null;

    public Nutrition NutritionOf(int amount) =>
        Unit == Unit.Piece ? Nutrition * amount : Nutrition * amount / 100;
}
