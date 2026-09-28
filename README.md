# resztka

[![CI](https://github.com/melihio/resztka/actions/workflows/ci.yml/badge.svg)](https://github.com/melihio/resztka/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

> Plan a week of meals on a fixed budget, without wasting the leftovers.

*resztka* (Polish: "leftover") takes a budget, a number of days and a catalog of
real supermarket products, and returns the most nutritious meal plan that money
can buy, plus the shopping list for it.

Recipe apps and LLMs assume you can buy 100 g of rice. You can't: rice comes in
400 g bags. resztka knows this. Once a recipe opens a bag, the rest of the bag is
free, so the planner uses it in other meals (rice pudding for breakfast, egg fried
rice on Thursday) instead of buying something new.

```
$ resztka --budget 100 --days 7 --meals breakfast,main,main

resztka · 7 days · 1 person · budget 100.00 zł · goal: most nutrition

Day 1                              1934 kcal · 72 g protein · 30 g fibre
  Breakfast  Rice pudding (sütlaç)               530 kcal · 18 g protein
  Main       Fried cabbage with kielbasa         702 kcal · 27 g protein
  Main       Red lentil soup (mercimek çorbası)  594 kcal · 27 g protein
...

Nutrition per person, daily average
                 average  lowest day    target
  Energy       1817 kcal   1550 kcal 2000 kcal     91%
  Protein           65 g        56 g      60 g    108%
  Fat               59 g        33 g
  Carbs            244 g       185 g
  Fibre             30 g        25 g      25 g    119%

Shopping list (Biedronka)
  1 × Cebula 1 kg                                                3.49 zł
  3 × Ciecierzyca konserwowa 400 g                              10.47 zł
  ...
────────────────────────────────────────────────────────────────────────
  Total                                                         98.59 zł
  Left of budget                                                 1.41 zł

Stays in the pantry for next week
  carrot 690 g, onion 450 g, rice 580 g, ...

Will spoil before the next shop
  bread 20 g, uht milk 2% 100 ml
```

## Features

- **Most nutrition for the money.** It fills the budget with the plan that best covers daily targets for energy, protein and fibre. Use `--goal cheapest` to spend as little as possible instead.
- **Nutrition facts.** Every meal and day shows kcal and protein, and a summary compares energy, protein, fat, carbs and fibre with the targets.
- **Pack-size aware.** It buys whole packs and picks the cheapest mix of sizes (one 1 kg bag or two 400 g bags).
- **Reuses leftovers.** Opened packs get used up by other recipes.
- **Shelf life.** Chicken is planned for the first days after the shop, not day 6.
- **Waste-averse.** Leftovers that would spoil before the next shop count as money thrown away. Onions that keep for weeks don't.
- **Diets and dislikes.** Avoid whole food categories (`--avoid pork,sugar`) or follow a diet (`--diet vegetarian`). When nothing fits, it tells you why.
- **Constraints.** Budget, meal slots per day, number of people, minimum kcal per day, max repeats per recipe, ingredients you already have, ingredients you don't eat.
- **Deterministic and explainable.** No LLM. Plain data and an integer program: the same input always gives an optimal plan.

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/melihio/resztka.git
cd resztka
dotnet run --project src/Resztka.Cli -- --budget 100
```

### Options

| Option | Default | Description |
| --- | --- | --- |
| `-b`, `--budget` | *required* | Maximum amount to spend, in zł |
| `-d`, `--days` | `7` | Number of days to plan, starting on the day of the shop |
| `-g`, `--goal` | `nutrition` | `nutrition`: most nutrition within budget. `cheapest`: lowest cost |
| `--kcal` | `2000` | Daily energy target per person (0 to ignore) |
| `--protein` | `60` | Daily protein target per person, g (0 to ignore) |
| `--fiber` | `25` | Daily fibre target per person, g (0 to ignore) |
| `-m`, `--meals` | `breakfast,main` | Meal slots of a day |
| `-p`, `--people` | `1` | People eating every meal |
| `--min-kcal` | none | Hard minimum kcal per person per day |
| `--max-repeats` | `3` | How often a single recipe may appear |
| `--pantry` | none | What you already have, e.g. `rice=300,eggs=4` |
| `-a`, `--avoid` | none | Food categories to avoid, e.g. `chicken,pork,beef,sugar` (see below) |
| `--diet` | none | `vegetarian`, `vegan` or `pescatarian` |
| `-x`, `--exclude` | none | Single ingredients to avoid, e.g. `minced-meat,kielbasa` |
| `--time-limit` | `10` | Maximum solver time in seconds |
| `--data` | bundled | Directory with a custom catalog |

Exit codes: `0` plan found, `1` no plan is possible (the output says why), `2` invalid input.

### Food categories

`chicken`, `pork`, `beef`, `fish`, `eggs`, `dairy`, `gluten`, `sugar`, `legumes`,
`vegetables`, `fruit`. An ingredient can belong to several (minced pork & beef is
both `pork` and `beef`), and a recipe is skipped if any of its ingredients is avoided.

| Diet | Avoids |
| --- | --- |
| `vegetarian` | chicken, pork, beef, fish |
| `vegan` | chicken, pork, beef, fish, eggs, dairy |
| `pescatarian` | chicken, pork, beef |

```bash
resztka --budget 100 --avoid chicken,pork,beef,sugar
resztka --budget 80 --diet vegan --days 5
```

## How it works

The week is modelled as an integer linear program and solved with
[Google OR-Tools CP-SAT](https://developers.google.com/optimization/cp/cp_solver).

**Variables**

- `x[r,d,s] ∈ {0,1}`: recipe *r* is cooked on day *d* in meal slot *s*
- `n[p] ∈ ℕ`: packs of product *p* to buy

**Constraints**

- every meal slot gets exactly one recipe
- per ingredient: `Σ amount used by chosen recipes ≤ Σ n[p] · pack size + pantry`
- `Σ n[p] · price ≤ budget`
- each recipe appears at most *max-repeats* times
- each day reaches *min-kcal*
- a recipe using a fresh ingredient can only be planned before that ingredient's shelf life ends

**Objective**, in two passes (`--goal nutrition`, the default):

1. Maximise `Σ c[d,n]`, where `c[d,n]` is how much of the target for nutrient *n*
   (kcal, protein, fibre) day *d* covers, **capped at 100%**. The cap means a
   4000 kcal day of oil and sugar earns nothing extra, and nothing is bought just
   to overshoot a target.
2. Keep that score and minimise cost plus the value of leftovers that spoil
   before the next shop.

With `--goal cheapest` only the second pass runs.

The leftover behaviour isn't a special rule. It falls out of the ingredient
constraint: once `n[rice] = 1` is paid for, using the other 300 g adds nothing to
the cost, so recipes that use it become the cheapest way to fill the remaining slots.

## Catalog

The catalog is plain JSON in [`data/`](data):

- [`ingredients.json`](data/ingredients.json): generic ingredients with unit, nutrition (kcal, protein, fat, carbs, fibre per 100 g/ml or per piece), shelf life and food categories
- [`recipes.json`](data/recipes.json): single-serving recipes that use ingredients
- [`products/biedronka.json`](data/products/biedronka.json): packs you can buy, with size and price

> ⚠️ Bundled prices are rounded approximations from September 2026. They show how the
> planner works but won't match the shelf exactly.

## Contributing

Contributions are very welcome, especially ones that don't need any C#:

- **Recipes.** Cheap, filling dishes. Polish, Turkish, Ukrainian, anything.
- **Prices.** Updates for `products/biedronka.json`, or a new store file (Lidl, Kaufland, Dino, …).
- **Code.** See the [open issues](https://github.com/melihio/resztka/issues).

See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## Roadmap

- [ ] Avoid the same meal twice in a day or on consecutive days
- [ ] Prefer recipes the user likes (ratings)
- [ ] Multiple stores with a "one shop only" option
- [ ] Web UI
- [ ] Fresh prices from store websites

## License

[MIT](LICENSE)
