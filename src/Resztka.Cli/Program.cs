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
    Description = "Number of days to plan.",
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

var minKcalOption = new Option<int?>("--min-kcal")
{
    Description = "Minimum energy per person per day.",
};

var maxRepeatsOption = new Option<int>("--max-repeats")
{
    Description = "How often a single recipe may appear in the plan.",
    DefaultValueFactory = _ => 3,
};

var pantryOption = new Option<string?>("--pantry")
{
    Description = "Ingredients already at home, e.g. rice=300,eggs=4.",
};

var excludeOption = new Option<string?>("--exclude", "-x")
{
    Description = "Comma separated ingredients to avoid, e.g. minced-meat,kielbasa.",
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
    minKcalOption,
    maxRepeatsOption,
    pantryOption,
    excludeOption,
    dataOption,
};

root.SetAction(parseResult =>
{
    try
    {
        var catalog = CatalogLoader.LoadFromDirectory(parseResult.GetValue(dataOption)!.FullName);

        var request = new PlanRequest
        {
            Budget = parseResult.GetValue(budgetOption),
            Days = parseResult.GetValue(daysOption),
            MealsPerDay = ArgumentParsing.ParseMeals(parseResult.GetValue(mealsOption)!),
            People = parseResult.GetValue(peopleOption),
            MinKcalPerDay = parseResult.GetValue(minKcalOption),
            MaxRepeatsPerRecipe = parseResult.GetValue(maxRepeatsOption),
            Pantry = ArgumentParsing.ParsePantry(parseResult.GetValue(pantryOption)),
            ExcludedIngredients = ArgumentParsing.ParseList(parseResult.GetValue(excludeOption)).ToHashSet(),
        };

        var plan = new MealPlanner(catalog).Plan(request);
        PlanRenderer.Write(Console.Out, catalog, request, plan);

        return plan.Status == PlanStatus.Infeasible ? ExitCodes.Infeasible : ExitCodes.Success;
    }
    catch (Exception ex) when (ex is ArgumentException or CatalogValidationException or IOException or FormatException)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return ExitCodes.InvalidInput;
    }
});

return root.Parse(args).Invoke();
