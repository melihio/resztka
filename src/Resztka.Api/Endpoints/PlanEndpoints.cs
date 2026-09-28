using Resztka.Api.Contracts;
using Resztka.Core.Catalog;
using Resztka.Core.Planning;

namespace Resztka.Api.Endpoints;

public static class PlanEndpoints
{
    public const int MaxTimeLimitSeconds = 30;

    public static IEndpointRouteBuilder MapPlanEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/plans", CreatePlan)
            .WithTags("Plans")
            .WithSummary("Plan meals and a shopping list for a budget.")
            .WithDescription(
                "Returns 200 with status optimal or feasible and the plan, or 200 with status infeasible " +
                "and the reason when no plan is possible. Invalid input returns 400.");

        return app;
    }

    private static IResult CreatePlan(CreatePlanRequest body, FoodCatalog catalog, MealPlanner planner)
    {
        if (body.TimeLimitSeconds is < 1 or > MaxTimeLimitSeconds)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["timeLimitSeconds"] = [$"Must be between 1 and {MaxTimeLimitSeconds}."],
            });
        }

        var avoided = (body.Avoid ?? []).ToHashSet();
        if (body.Diet is { } diet)
            avoided.UnionWith(Diets.ExcludedCategories(diet));

        var defaults = new PlanRequest { Budget = body.Budget };
        var request = defaults with
        {
            Days = body.Days ?? defaults.Days,
            MealsPerDay = body.Meals ?? defaults.MealsPerDay,
            People = body.People ?? defaults.People,
            Goal = body.Goal ?? defaults.Goal,
            Targets = body.Targets is { } t ? new NutritionTargets(t.Kcal, t.Protein, t.Fiber) : defaults.Targets,
            MinKcalPerDay = body.MinKcalPerDay,
            MaxRepeatsPerRecipe = body.MaxRepeatsPerRecipe ?? defaults.MaxRepeatsPerRecipe,
            DailyDrinks = body.Drinks ?? defaults.DailyDrinks,
            Pantry = body.Pantry ?? defaults.Pantry,
            ExcludedIngredients = (body.ExcludeIngredients ?? []).ToHashSet(),
            ExcludedCategories = avoided,
            TimeLimit = TimeSpan.FromSeconds(body.TimeLimitSeconds ?? defaults.TimeLimit.TotalSeconds),
        };

        MealPlan plan;
        try
        {
            plan = planner.Plan(request);
        }
        catch (ArgumentException ex)
        {
            var message = ex.ParamName is { } param ? ex.Message.Replace($" (Parameter '{param}')", "") : ex.Message;
            return Results.Problem(message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid plan request");
        }

        return Results.Ok(PlanResponse.From(plan, request, catalog));
    }
}
