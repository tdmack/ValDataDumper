# Changelog

Versions track the plugin, not the game. The Valheim version a dump came from is recorded in
`manifest.json` and stamped into every generated markdown file.

## 0.10.0

Says which item each recipe makes, and dumps weapon attacks. All changes are additive: no
existing key moved or changed.

- **`recipe-stations.json`** gains `item` (the prefab the recipe makes — `m_item`), `amount`
  (how many one craft makes) and `enabled`. The file is keyed by recipe name minus `Recipe_`,
  which is not always the item's prefab (`Recipe_Battleaxe_Crystal` makes `BattleaxeCrystal`);
  joining on the key picked the wrong prefab wherever several share a display name.
- **`stats-dump.json`** gains `attacks: { primary, secondary }` on every item: attack type and
  animation, damage, stagger and force multipliers, stamina and eitr cost. With them, a weapon's
  stagger per hit can be computed from its damage.

Tested against Valheim 1.0.15.

## 0.9.0

Adds the status effects items refer to. All changes are additive: no existing key moved or
changed.

- **New `status-effects.json`**: every set bonus, equip effect and consume effect (meads, food)
  that an item names in `item-extras.json`, keyed by effect prefab name. Each row has the
  localized name and tooltip, the duration, and — for `SE_Stats` effects — the modifiers that say
  what the effect does: stamina use (run, jump, attack, block, dodge, swim, sneak), health,
  stamina and eitr regen, up-front and over-time restores, up to two skill-level boosts, per-type
  damage bonuses, damage-taken modifiers, speed and carry weight.
- **`manifest.json`** gains a `statusEffects` count.

Set-bonus tooltips are often flavour ("Makes you more sneaky."); the numbers live only on the
effect object, so this is the only way to know what a set bonus actually does.

Tested against Valheim 1.0.15.

## 0.8.0

Adds the Fermenter's configuration and the any-one-ingredient recipe flag. All changes are
additive: no existing key moved or changed.

- **`piece-extras.json` → `fermenter`**: every `Fermenter` component's fermentation time and
  conversions (`from` mead base → `to` mead, with `producedItems` per batch); `null` on every
  other piece. In Valheim 1.0.15 the Fermenter has 20 conversions at 2400 s each, and every mead
  yields 6 except Berserker mead, which yields 3.
- **`recipe-stations.json` → `requireOnlyOneIngredient`**: the recipe takes any one of its listed
  resources, not all of them. Raw Fish (`Fish1`) is the only one in 1.0.15; it lists every fish.
- **`recipe-stations.json` → `qualityResultAmountMultiplier`**: how much a higher-quality
  ingredient raises an any-one-ingredient recipe's output (3 for Raw Fish; 1 elsewhere).

As with 0.7.0, the Fermenter's numbers exist only on the prefab (the in-code initializers are
4 per batch and 2400 s), so a live dump is the only source to trust.

Tested against Valheim 1.0.15.

## 0.7.0

Adds converter configurations and the upgrade-only flag. All changes are additive: no existing
key moved or changed.

- **`piece-extras.json` → `smelter`**: every `Smelter` component's conversions (`from` → `to`
  prefabs), fuel item, fuel per product, seconds per product and capacities; `null` on every other
  piece. A conversion `from` of **`null`** marks a no-source converter (the game's
  `m_noSourceConversion`), which turns fuel alone into output. Valheim 1.0's **Frigid Kiln** works
  this way: 5 Ice → 1 Liquid Frost.
- **`piece-extras.json` → `cookingStation`**: every `CookingStation` component's conversions
  (with `cookTime`), fuel item, seconds per fuel unit, slots, and the `useFuelWhileEmpty` /
  `requireFire` flags; `null` elsewhere. Valheim 1.0's **Frost Foundry** is a cooking station. It
  hardens a Nord *Cast* into its finished item, burning Liquid Frost by time, so a Cast costs
  `cookTime / secPerFuel`, which is 5 in 1.0.15. `useFuelWhileEmpty` corrects the game's spelling
  (`m_useFueldWhileEmpty`).
- **`recipe-stations.json` → `noCraftOnlyUpgrade`**: the recipe is hidden from the craft list but
  still drives upgrades. Every finished Nord item sets it, and patch 1.0.14 fixed a missing flag on
  Helmet of the Protector, which is the kind of drift this makes visible.

None of these numbers exist in code: the initializers (`m_fuelPerProduct = 4`,
`m_secPerFuel = 5000`, …) are not the game's values. They live only on the prefabs, so a live dump
is the only source that can be trusted for which Cast becomes which item and what it costs.

Tested against Valheim 1.0.15.

## 0.6.0

Adds the **Upgrader (Refinement Forge)** block to `item-extras.json` — the four `SharedData`
fields that decide what a refinement attempt at the Forge of Potential does:

- `upgradeChance` — probability the attempt succeeds.
- `breakChance` — probability it destroys the item (rolled against the remainder, not the whole).
- `successUpgradeSteps` — how many levels a success grants.
- `breakReturnIngredientsAmount` — the fraction of recoverable requirements refunded on a break.
  (Key spelling corrected; the game's field is `m_breakReturnIngreientsAmount`.)

Valheim 1.0 lists an idol in nearly every gear recipe, and it is **not a crafting cost** — it is
spendable only at the Forge of Potential, which charges the idol and nothing else. The odds above
are the only game-authored description of what that costs you, and their in-code initializers
(0.65 / 0.1 / 1 / 0.5) are per-prefab overridable, so a live dump is the only honest source for a
given idol's real numbers. Without them a consumer has to either omit the risk or invent it.

Emitted for every item rather than only idols, since the fields sit on every `SharedData` and a
consumer should not have to guess which rows carry them.

Also closes two gaps in `stats-dump.json` that stopped a consumer computing an item's stats at a
level the source data did not already enumerate — which the Forge of Potential makes reachable,
since it ignores `maxQuality` entirely:

- `deflectionForcePerLevel` — `SlimmedItem` shipped `armorPerLevel`, `damagePerLevel`,
  `blockPowerPerLevel` and `durabilityPerLevel` but omitted this one, leaving **parry force** the
  single displayed stat with no computable value above the enumerated levels.
- `scaleWeightByQuality` — weight is not flat across levels; `GetWeight` multiplies by
  `1 + (quality - 1) * m_scaleWeightByQuality`.

Both sit beside their siblings rather than in `item-extras.json`: splitting per-level stats across
two files to preserve a defunct tool's exact key set would cost more than the compatibility is
worth. `stats-dump.json` is therefore a **documented superset** of `SlimmedItem` from 0.6.0 —
still additive, with no existing key moved or changed. `m_scaleByQuality` is deliberately *not*
dumped: it scales the mesh, not a stat.

## 0.5.0

First public release. Verified against **Valheim 1.0.7** with **BepInEx 5.4.23.5**.

- Jötunn-format `item-list.md`, `recipe-list.md` and `piece-list.md`, written from the live
  `ObjectDB` and every `PieceTable` reachable from it.
- Icon export for items and pieces (`RenderTexture` readback).
- `recipe-stations.json` — prefab to crafting-station token and minimum station level.
- `stats-dump.json` in WackysDatabase's `SlimmedItem` shape, so it drops in where that fixture
  was used.
- `item-extras.json` and `piece-extras.json` for facets the markdown grammar has no column for
  (stack size, teleportability, vendor value, tool tier, set effects; comfort, container size,
  build station).
- `localization.json` — every `$token` encountered, resolved to English.
- Auto-dump on world load, plus a configurable hotkey (default **F9**) to re-run.

Written to close the gap between a game release and a refreshed dataset. It uses no Harmony
patches, no ServerSync and no publicizer — it only reads public game state, so it has few moving
parts exposed to a major release.
