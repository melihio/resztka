using Resztka.Core.Catalog;

namespace Resztka.Cli;

internal static class ArgumentParsing
{
    public static IReadOnlyList<string> ParseList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static IReadOnlyList<MealType> ParseMeals(string value) =>
        ParseList(value)
            .Select(meal => Enum.TryParse<MealType>(meal, ignoreCase: true, out var type)
                ? type
                : throw new FormatException(
                    $"Unknown meal '{meal}'. Use one of: {string.Join(", ", Enum.GetNames<MealType>().Select(n => n.ToLowerInvariant()))}."))
            .ToList();

    public static IReadOnlySet<FoodCategory> ParseCategories(string? value) =>
        ParseList(value)
            .Select(category => Enum.TryParse<FoodCategory>(category, ignoreCase: true, out var parsed)
                ? parsed
                : throw new FormatException(
                    $"Unknown category '{category}'. Use one of: {string.Join(", ", Enum.GetNames<FoodCategory>().Select(n => n.ToLowerInvariant()))}."))
            .ToHashSet();

    public static IReadOnlyDictionary<string, int> ParsePantry(string? value) =>
        ParseAmounts(value, "pantry entry", "ingredient=amount, e.g. rice=300");

    public static IReadOnlyDictionary<string, int> ParseDrinks(string? value) =>
        ParseAmounts(value, "drink", "drink=servings per day, e.g. coffee=2");

    private static Dictionary<string, int> ParseAmounts(string? value, string what, string expected)
    {
        var amounts = new Dictionary<string, int>();
        foreach (var entry in ParseList(value))
        {
            var parts = entry.Split('=', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var amount) || amount <= 0)
                throw new FormatException($"Invalid {what} '{entry}'. Expected {expected}.");

            amounts[parts[0]] = amounts.GetValueOrDefault(parts[0]) + amount;
        }

        return amounts;
    }
}
