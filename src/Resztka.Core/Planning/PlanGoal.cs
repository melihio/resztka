using Resztka.Core.Catalog;

namespace Resztka.Core.Planning;

public enum PlanGoal
{
    MaxNutrition,

    Cheapest,
}

public sealed record NutritionTargets(double Kcal, double Protein, double Fiber)
{
    public static NutritionTargets Adult { get; } = new(2000, 60, 25);

    public IEnumerable<(Nutrient Nutrient, double Target)> All()
    {
        yield return (Nutrient.Kcal, Kcal);
        yield return (Nutrient.Protein, Protein);
        yield return (Nutrient.Fiber, Fiber);
    }
}
