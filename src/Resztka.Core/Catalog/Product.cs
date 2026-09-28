namespace Resztka.Core.Catalog;

public sealed record Product(
    string Id,
    string Name,
    string IngredientId,
    int PackSize,
    decimal Price,
    string Store)
{
    public string? NameEn { get; init; }
}
