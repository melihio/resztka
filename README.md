# resztka

[![CI](https://github.com/melihio/resztka/actions/workflows/ci.yml/badge.svg)](https://github.com/melihio/resztka/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

> Plan a week of meals on a fixed budget, without wasting the leftovers.

*resztka* (Polish: "leftover") takes a budget, a number of days and a catalog of
real supermarket products, and returns a meal plan plus a shopping list.

Recipe apps and LLMs assume you can buy 100 g of rice. You can't: rice comes in
400 g bags. resztka knows this. Once a recipe opens a bag, the rest of the bag is
free, so the planner uses it in other meals (rice pudding for breakfast, egg fried
rice on Thursday) instead of buying something new.

```
$ resztka --budget 100 --meals breakfast,main,main --min-kcal 1800 --pantry oil=500,sugar=200

resztka · 7 days · 1 person · budget 100.00 zł

Day 1                                                  1826 kcal
  Breakfast  Rice pudding (sütlaç)
  Main       Fried cabbage with kielbasa
  Main       Red lentil soup (mercimek çorbası)
...

Shopping list (Biedronka)
  1 × Cebula 1 kg                                        3.49 zł
  1 × Chleb pszenno-żytni 500 g                          3.99 zł
  ...
────────────────────────────────────────────────────────────────
  Total                                                 87.61 zł
  Left of budget                                        12.39 zł

Stays in the pantry for next week
  carrot 690 g, onion 450 g, potatoes 1050 g, rice 20 g, ...

Will spoil before the next shop
  bread 20 g, hard cheese (sliced) 90 g, uht milk 2% 100 ml
```

## Features

- **Pack-size aware.** It buys whole packs and picks the cheapest mix of sizes (one 1 kg bag or two 400 g bags).
- **Reuses leftovers.** Opened packs get used up by other recipes.
- **Shelf life.** Chicken is planned for the first days after the shop, not day 6.
- **Waste-averse.** Leftovers that would spoil before the next shop count as money thrown away. Onions that keep for weeks don't.
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
| `-m`, `--meals` | `breakfast,main` | Meal slots of a day |
| `-p`, `--people` | `1` | People eating every meal |
| `--min-kcal` | none | Minimum kcal per person per day |
| `--max-repeats` | `3` | How often a single recipe may appear |
| `--pantry` | none | What you already have, e.g. `rice=300,eggs=4` |
| `-x`, `--exclude` | none | Ingredients to avoid, e.g. `minced-meat,kielbasa` |
| `--data` | bundled | Directory with a custom catalog |

Exit codes: `0` plan found, `1` no plan fits the budget, `2` invalid input.

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

**Objective:** minimise cost plus the value of leftovers that spoil before the next shop.

The leftover behaviour isn't a special rule. It falls out of the ingredient
constraint: once `n[rice] = 1` is paid for, using the other 300 g adds nothing to
the cost, so recipes that use it become the cheapest way to fill the remaining slots.

## Catalog

The catalog is plain JSON in [`data/`](data):

- [`ingredients.json`](data/ingredients.json): generic ingredients with unit, kcal and shelf life
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

- [ ] Avoid the same meal on consecutive days
- [ ] Prefer recipes the user likes (ratings)
- [ ] Multiple stores with a "one shop only" option
- [ ] Web UI
- [ ] Fresh prices from store websites

## License

[MIT](LICENSE)
