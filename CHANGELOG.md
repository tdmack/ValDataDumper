# Changelog

Versions track the plugin, not the game. The Valheim version a dump came from is recorded in
`manifest.json` and stamped into every generated markdown file.

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
