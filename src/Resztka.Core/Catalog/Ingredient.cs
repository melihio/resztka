namespace Resztka.Core.Catalog;

public sealed record Ingredient(
    string Id,
    string Name,
    Unit Unit,
    double KcalPerUnit,
    int? ShelfLifeDays = null)
{
    public bool IsPerishable => ShelfLifeDays is not null;
}
