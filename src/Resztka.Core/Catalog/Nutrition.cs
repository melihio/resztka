namespace Resztka.Core.Catalog;

public enum Nutrient
{
    Kcal,
    Protein,
    Fat,
    Carbs,
    Fiber,
}

public readonly record struct Nutrition(double Kcal, double Protein, double Fat, double Carbs, double Fiber)
{
    public double this[Nutrient nutrient] => nutrient switch
    {
        Nutrient.Kcal => Kcal,
        Nutrient.Protein => Protein,
        Nutrient.Fat => Fat,
        Nutrient.Carbs => Carbs,
        Nutrient.Fiber => Fiber,
        _ => throw new ArgumentOutOfRangeException(nameof(nutrient)),
    };

    public double KcalFromMacros => 4 * Protein + 9 * Fat + 4 * Carbs + 2 * Fiber;

    public static Nutrition operator +(Nutrition a, Nutrition b) =>
        new(a.Kcal + b.Kcal, a.Protein + b.Protein, a.Fat + b.Fat, a.Carbs + b.Carbs, a.Fiber + b.Fiber);

    public static Nutrition operator *(Nutrition n, double factor) =>
        new(n.Kcal * factor, n.Protein * factor, n.Fat * factor, n.Carbs * factor, n.Fiber * factor);

    public static Nutrition operator /(Nutrition n, double divisor) => n * (1 / divisor);
}
