using Resztka.Core.Catalog;

namespace Resztka.Core.Planning;

public sealed record PlanRequest
{
    public required decimal Budget { get; init; }

    public int Days { get; init; } = 7;

    public IReadOnlyList<MealType> MealsPerDay { get; init; } = [MealType.Breakfast, MealType.Main];

    public int People { get; init; } = 1;

    public int MaxRepeatsPerRecipe { get; init; } = 3;

    public int? MinKcalPerDay { get; init; }

    public IReadOnlyDictionary<string, int> Pantry { get; init; } = new Dictionary<string, int>();

    public IReadOnlySet<string> ExcludedIngredients { get; init; } = new HashSet<string>();

    public TimeSpan TimeLimit { get; init; } = TimeSpan.FromSeconds(10);
}
