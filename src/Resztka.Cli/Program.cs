using System.CommandLine;
using System.Text;
using Resztka.Cli;
using Resztka.Core.Catalog;
using Resztka.Core.Planning;

Console.OutputEncoding = Encoding.UTF8;

var budgetOption = new Option<decimal>("--budget", "-b")
{
    Description = "Maximum amount to spend, in zł.",
    Required = true,
};

var daysOption = new Option<int>("--days", "-d")
{
    Description = "Number of days to plan, starting on the day of the shop.",
    DefaultValueFactory = _ => 7,
};

var mealsOption = new Option<string>("--meals", "-m")
{
    Description = "Comma separated meal slots of a day, e.g. breakfast,main,main.",
    DefaultValueFactory = _ => "breakfast,main",
};

var peopleOption = new Option<int>("--people", "-p")
{
    Description = "Number of people eating every meal.",
    DefaultValueFactory = _ => 1,
};

var goalOption = new Option<string>("--goal", "-g")
{
    Description = "nutrition: the most nutrition the budget can buy. cheapest: spend as little as possible.",
    DefaultValueFactory = _ => "nutrition",
};
goalOption.AcceptOnlyFromAmong("nutrition", "cheapest");

var kcalOption = new Option<double>("--kcal")
{
    Description = "Daily energy target per person, in kcal (0 to ignore).",
    DefaultValueFactory = _ => NutritionTargets.Adult.Kcal,
};

var proteinOption = new Option<double>("--protein")
{
    Description = "Daily protein target per person, in grams (0 to ignore).",
    DefaultValueFactory = _ => NutritionTargets.Adult.Protein,
};

var fiberOption = new Option<double>("--fiber")
{
    Description = "Daily fibre target per person, in grams (0 to ignore).",
    DefaultValueFactory = _ => NutritionTargets.Adult.Fiber,
};

var minKcalOption = new Option<int?>("--min-kcal")
{
    Description = "Hard minimum energy per person per day; no plan below it is accepted.",
};

var maxRepeatsOption = new Option<int>("--max-repeats")
{
    Description = "How often a single recipe may appear in the plan.",
    DefaultValueFactory = _ => 3,
};

var drinksOption = new Option<string?>("--drinks")
{
    Description = "Drinks each person has every day, e.g. coffee-milk=2,orange-juice=1. "
        + "Choose from: tea, tea-sugar, coffee, coffee-milk, orange-juice, kefir, water.",
};

var pantryOption = new Option<string?>("--pantry")
{
    Description = "Ingredients already at home, e.g. rice=300,eggs=4.",
};

var excludeOption = new Option<string?>("--exclude", "-x")
{
    Description = "Comma separated ingredients to avoid, e.g. minced-meat,kielbasa.",
};

var avoidOption = new Option<string?>("--avoid", "-a")
{
    Description = "Comma separated food categories to avoid: "
        + string.Join(", ", Enum.GetNames<FoodCategory>().Select(n => n.ToLowerInvariant())) + ".",
};

var dietOption = new Option<string?>("--diet")
{
    Description = "vegetarian (no meat or fish), vegan (no animal products) or pescatarian (no meat).",
};
dietOption.AcceptOnlyFromAmong("vegetarian", "vegan", "pescatarian");

var timeLimitOption = new Option<double>("--time-limit")
{
    Description = "Maximum solver time in seconds.",
    DefaultValueFactory = _ => 10,
};

var dataOption = new Option<DirectoryInfo>("--data")
{
    Description = "Catalog directory with ingredients.json, recipes.json and products/.",
    DefaultValueFactory = _ => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "data")),
};

var root = new RootCommand("resztka — plan a week of meals on a fixed budget, without wasting the leftovers.")
{
    budgetOption,
    daysOption,
    mealsOption,
    peopleOption,
    goalOption,
    kcalOption,
    proteinOption,
    fiberOption,
    minKcalOption,
    maxRepeatsOption,
    drinksOption,
    pantryOption,
    excludeOption,
    avoidOption,
    dietOption,
    timeLimitOption,
    dataOption,
};

root.SetAction(parseResult =>
{
    try
    {
        var catalog = CatalogLoader.LoadFromDirectory(parseResult.GetValue(dataOption)!.FullName);

        var avoided = ArgumentParsing.ParseCategories(parseResult.GetValue(avoidOption)).ToHashSet();
        if (parseResult.GetValue(dietOption) is { } diet)
            avoided.UnionWith(Diets.ExcludedCategories(Enum.Parse<Diet>(diet, ignoreCase: true)));

        var request = new PlanRequest
        {
            Budget = parseResult.GetValue(budgetOption),
            Days = parseResult.GetValue(daysOption),
            MealsPerDay = ArgumentParsing.ParseMeals(parseResult.GetValue(mealsOption)!),
            People = parseResult.GetValue(peopleOption),
            Goal = parseResult.GetValue(goalOption) == "cheapest" ? PlanGoal.Cheapest : PlanGoal.MaxNutrition,
            Targets = new NutritionTargets(
                parseResult.GetValue(kcalOption),
                parseResult.GetValue(proteinOption),
                parseResult.GetValue(fiberOption)),
            MinKcalPerDay = parseResult.GetValue(minKcalOption),
            MaxRepeatsPerRecipe = parseResult.GetValue(maxRepeatsOption),
            DailyDrinks = ArgumentParsing.ParseDrinks(parseResult.GetValue(drinksOption)),
            Pantry = ArgumentParsing.ParsePantry(parseResult.GetValue(pantryOption)),
            ExcludedIngredients = ArgumentParsing.ParseList(parseResult.GetValue(excludeOption)).ToHashSet(),
            ExcludedCategories = avoided,
            TimeLimit = TimeSpan.FromSeconds(parseResult.GetValue(timeLimitOption)),
        };

        var plan = new MealPlanner(catalog).Plan(request);
        PlanRenderer.Write(Console.Out, catalog, request, plan);

        return plan.Status == PlanStatus.Infeasible ? ExitCodes.Infeasible : ExitCodes.Success;
    }
    catch (Exception ex) when (ex is ArgumentException or CatalogValidationException or IOException or FormatException)
    {
        var message = ex is ArgumentException { ParamName: { } param }
            ? ex.Message.Replace($" (Parameter '{param}')", "")
            : ex.Message;
        Console.Error.WriteLine($"error: {message}");
        return ExitCodes.InvalidInput;
    }
});

return root.Parse(args).Invoke();
