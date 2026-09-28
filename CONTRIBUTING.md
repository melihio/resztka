# Contributing to resztka

Thanks for helping! Most contributions are data, not code.

## Adding a recipe

1. Add an entry to [`data/recipes.json`](data/recipes.json):

   ```json
   {
     "id": "lentil-soup",
     "name": "Red lentil soup (mercimek çorbası)",
     "mealTypes": ["main"],
     "ingredients": { "red-lentils": 80, "onion": 50, "carrot": 50, "oil": 10, "bread": 80 }
   }
   ```

   - Amounts are for **one serving**, in the ingredient's unit (grams, millilitres or pieces).
   - Leave out salt, pepper and dried spices. They're assumed to be at home.
   - `mealTypes` is `breakfast`, `main` or both.

2. If the recipe needs a new ingredient, add it to [`data/ingredients.json`](data/ingredients.json)
   with its unit, `nutrition`, `categories` (e.g. `["pork", "beef"]`, so diet filters work)
   and `shelfLifeDays` (leave it out for dry or frozen goods), and add at least one
   product for it in a store file.

   Copy `nutrition` straight from a Polish/EU label: values per 100 g or 100 ml (per piece
   for `piece` ingredients), with carbs *excluding* fibre:

   ```json
   "nutrition": { "kcal": 350, "protein": 7, "fat": 0.6, "carbs": 78, "fiber": 1.3 }
   ```

3. Run the tests. They check that every ingredient can be bought and that kcal roughly
   matches the macronutrients, which catches most typos:

   ```bash
   dotnet test
   ```

## Updating prices or adding a store

Store files live in [`data/products/`](data/products). Each product maps a real pack to an ingredient:

```json
{ "id": "bdr-rice-400", "name": "Ryż długoziarnisty 400 g", "nameEn": "Long-grain rice 400 g", "ingredient": "rice", "packSize": 400, "price": 3.99 }
```

`name` is exactly what the shelf label says; `nameEn` is its English translation.

For canned food, `packSize` is the **drained** weight you actually cook with.
Please update `pricesAsOf` when you refresh prices.

## Code

- Run `dotnet format` before committing. CI checks formatting.
- Add tests for planner changes in `tests/Resztka.Core.Tests`.
- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:` …).
