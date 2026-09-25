# ValDataDumper

A **read-only BepInEx 5 plugin** that dumps Valheim's item, recipe and build-piece data straight
from the running game — in the same format [Jötunn](https://github.com/Valheim-Modding/Jotunn)'s
JotunnDoc produces, plus icons, crafting stations, item stats and a localization table.

Point it at a world, press nothing, and you get a complete, current dataset.

## Why this exists

Tools that depend on Valheim game data — calculators, wikis, planners — usually get it from a
generated documentation dump. Those dumps are produced manually, so there is always a gap between
a game release and a refreshed dataset. This closes the gap: it reads the data directly from the
running game, so a current dataset is one world-load away, on your schedule rather than anyone
else's.

The design choice that keeps it simple is worth stating up front:

> **No Harmony patches. No ServerSync. No publicizer.**

It only reads public game state. Content-*registration* APIs are the ones most exposed to change
across a major release; reading state is far less so. It also means contributing to it needs
nothing but the stock game DLLs and BepInEx — no publicized-assembly prebuild step.

## Install

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx) for Valheim, or use a mod manager
   (r2modman, Thunderstore Mod Manager) and let it do that.
2. Download `ValDataDumper.dll` from the
   [latest release](https://github.com/tdmack/ValDataDumper/releases/latest).
3. Drop it in `BepInEx/plugins/ValDataDumper/`.

## Run

**Start the game modded and load any world.** That is all — the dump runs automatically once the
world has finished loading, because the `ObjectDB` is only complete in-world. Press **F9** to run
it again.

Check `BepInEx/LogOutput.log` — you should see it load, then report what it wrote:

```
[Info   :ValDataDumper] ValDataDumper 0.6.0 loaded; output → ...\BepInEx\config\valdatadumper
[Info   :ValDataDumper] [world-load] Valheim 1.0.7: items=1517 recipes=478 pieceTables=4 pieces=549 icons=1543 iconFailures=0 tokens=2544 stats=1517 itemExtras=1517 pieceExtras=422 → ...
```

Output lands in `BepInEx/config/valdatadumper/`.

### Configuration

`BepInEx/config/com.tdmack.valdatadumper.cfg`, created on first run:

| Setting | Default | What it does |
|---|---|---|
| `DumpOnWorldLoad` | `true` | Dump automatically the first time a world finishes loading |
| `ExportIcons` | `true` | Export item and piece icons as PNGs |
| `OutputDir` | `BepInEx/config/valdatadumper` | Where everything is written |
| `VersionOverride` | *(empty)* | Force the `generated from Valheim X.Y.Z` stamp; empty reads it from the game |
| `Hotkey` | `F9` | Re-run the dump in-world |

## Output

```
data/objects/item-list.md     Jötunn item-list format: Item | AssetID | Token | English Name | Type | Description
data/objects/recipe-list.md   Jötunn recipe-list format, single-level or `Level N:` grammar
data/pieces/piece-list.md     one `## <PieceTable>` section per build tool
images/items/<prefab>.png     icons, referenced from item-list as ../../images/items/...
images/pieces/<prefab>.png
recipe-stations.json          recipe -> crafting-station token, minimum station level,
                              upgrade-only flag (noCraftOnlyUpgrade), any-one-ingredient flag
                              (requireOnlyOneIngredient, qualityResultAmountMultiplier)
stats-dump.json               per-item stats, a documented superset of WackysDatabase's SlimmedItem
item-extras.json              stack size, teleportability, vendor value, tool tier, set effects,
                              upgrader odds (refinement forge)
piece-extras.json             comfort, container size, build station, and converter configs
                              (smelter / cookingStation / fermenter: conversions, fuel, yields, timing)
status-effects.json           every status effect an item refers to (set bonus, equip, consume):
                              name, tooltip, duration, and the SE_Stats modifiers
localization.json             every $token encountered -> English
manifest.json                 game version, timestamp, counts
```

Every generated markdown file carries a header stamp naming the game version it came from, and
`manifest.json` records the same version — so a half-finished or mismatched dump is detectable.

### Converter configs (`piece-extras.json`, 0.7.0+)

Every piece carries a `smelter` and a `cookingStation` key, and from 0.8.0 a `fermenter` key.
Each holds the configuration of that
component, or `null` when the piece does not have one. Items are prefab names, which you can join
to `item-list.md`.

**`smelter`** covers the Smelter, Blast Furnace, Charcoal Kiln, Eitr Refinery, Spinning Wheel,
Windmill and Frigid Kiln. Each product burns `fuelPerProduct` of `fuelItem`; `fuelItem` is `null`
when there is no fuel.

```json
"smelter": {
  "fuelItem": "Coal", "fuelPerProduct": 2, "secPerProduct": 30, "maxOre": 10, "maxFuel": 20,
  "conversions": [{ "from": "CopperOre", "to": "Copper" }]
}
```

A `from` of `null` marks a **no-source** converter, which turns fuel alone into output. Valheim
1.0's Frigid Kiln is one: `{ "fuelItem": "Ice", "fuelPerProduct": 5, … "conversions": [{ "from":
null, "to": "FrozenFuel" }] }`, so 5 Ice makes 1 Liquid Frost.

**`cookingStation`** covers cooking stations, the Stone Oven and Valheim 1.0's Frost Foundry.
Fuel burns **by time**, at `1 / secPerFuel` units per second while the station works, and all
occupied `slots` share it. `useFuelWhileEmpty` means it keeps burning with nothing inside.
`requireFire` means it needs a lit fire underneath instead.

```json
"cookingStation": {
  "useFuel": true, "fuelItem": "FrozenFuel", "secPerFuel": 10, "maxFuel": 20,
  "useFuelWhileEmpty": false, "requireFire": false, "slots": 1,
  "conversions": [{ "from": "SwordGoldUncooked", "to": "SwordGold", "cookTime": 50 }]
}
```

The Frost Foundry hardens a Nord *Cast* into its finished item. With one slot and no idle burn,
a Cast costs exactly `cookTime / secPerFuel` Liquid Frost (5 in Valheim 1.0.15).

**`fermenter`** (0.8.0+) covers the Fermenter, which turns a mead base into meads. It holds one
item at a time and burns no fuel: after `fermentationDuration` seconds, tapping it gives
`producedItems` of `to` for the one `from` put in.

```json
"fermenter": {
  "fermentationDuration": 2400,
  "conversions": [{ "from": "MeadBaseHealthMinor", "to": "MeadHealthMinor", "producedItems": 6 }]
}
```

In Valheim 1.0.15 every mead yields 6 per batch except Berserker mead (`MeadBzerker`), which
yields 3.

### Upgrade-only recipes (`recipe-stations.json`, 0.7.0+)

`noCraftOnlyUpgrade: true` means the recipe is hidden from the craft list but still drives
upgrades. Every finished Nord item sets it, because its level 1 comes from the Frost Foundry
instead.

This file is keyed by **recipe** name with the `Recipe_` prefix stripped, and that is not always
the crafted item's prefab. For example, `ArmorGoldChest` crafts `ArmorDeepNorthHeavyChest`. Join
through `recipe-list.md` to get the item.

### Any-one-ingredient recipes (`recipe-stations.json`, 0.8.0+)

`requireOnlyOneIngredient: true` means the recipe takes **any one** of its listed resources, not
all of them: crafting uses the first one you hold. `recipe-list.md` still lists every resource,
so summing them over-counts. In Valheim 1.0.15 only Raw Fish (`Fish1`) works this way, and it
lists every fish.

For these recipes the output grows with the ingredient's quality:
`amount + ceil((quality − 1) × amount × qualityResultAmountMultiplier)`. Raw Fish's multiplier is
3, so a higher-star fish gives more Raw Fish. The multiplier is dumped for every recipe but is 1,
and unused, everywhere else.

### Status effects (`status-effects.json`, 0.9.0+)

Every status effect an item refers to — its set bonus (`setStatusEffect` in `item-extras.json`),
its equip effect and its consume effect (meads, food) — keyed by the effect's prefab name, the same
name `item-extras.json` uses. Each row has the localized `name` and `tooltip`, the effect's C#
`type`, its duration `ttl` in seconds, and `stats` for `SE_Stats` effects (`null` otherwise).

`stats` holds raw game values. A multiplier (`healthRegenMultiplier`, …) of **1** and a modifier
(`runStaminaDrainModifier`, …) of **0** mean "no change". A modifier is a fraction: `-0.1` is −10%.
`skillLevel`/`skillLevel2` are skill ids (`None` when unused) raised by `skillLevelModifier`/
`skillLevelModifier2`. `percentDamage` is a per-type damage bonus (`0.1` = +10%), and `mods` are
damage-**taken** modifiers such as `{ "type": "Fire", "modifier": "Resistant" }`.

The Troll armour set bonus, with its neutral fields left out:

```json
"SetEffect_TrollArmor": {
  "name": "Sneaky", "tooltip": "Makes you more sneaky.", "type": "SE_Stats", "ttl": 0,
  "stats": { "skillLevel": "Sneak", "skillLevelModifier": 15, … }
}
```

The tooltip alone doesn't say "+15"; only the stats do.

## Compatibility

Output is intended as a drop-in for JotunnDoc's, with **one documented difference**:

> **`AssetID` is a deterministic 32-hex stand-in, not Unity's real AssetID.**
>
> The real asset GUID is not readable at runtime. The column is generated as an MD5 of the prefab
> name: stable across runs and unique per prefab, but **not** equal to what JotunnDoc emits. If
> you diff this output against a real JotunnDoc dump, that column will differ for every row and
> the tool is not broken.
>
> If you rely on AssetID matching Unity's, this tool cannot serve you. Nothing known consumes it —
> parsers expect the column to exist, not to mean anything.

`stats-dump.json` carries every `StatsDump` / `SlimmedItem` key WackysDatabase produced, with the
same names and meanings, so consumers of that fixture need no change. Since **0.6.0** it is a
**superset**: it adds `deflectionForcePerLevel` and `scaleWeightByQuality`, the two quality-scaling
fields `SlimmedItem` omitted — without them parry force and weight cannot be computed at any level
the source data does not already enumerate, which the Forge of Potential makes reachable. A parser
that ignores unknown keys is unaffected.

**Tested against Valheim 1.0.7, 1.0.12 and 1.0.15 with BepInEx 5.4.23.5.** It reads only public game state, so it is
likely to keep working across patches — but a release that moves a type between assemblies will
need a rebuild, and Valheim 1.0 did exactly that (`Localization` moved to `assembly_guiutils`).

## Scope

**This is a read-only data dumper, not a mod framework.** It reads game state and writes files.
That is the whole of it.

Deliberately out of scope: changing game behaviour or content, registering items or recipes,
runtime configuration of the game, networking or server sync, and any output format beyond the
ones above. If you need those, you want [Jötunn](https://github.com/Valheim-Modding/Jotunn).

Additional output formats and fields are considered on their merits, but the no-Harmony,
no-publicizer, read-only constraint is not up for negotiation — it is what keeps the tool small
enough to fix quickly when a release does move something.

## Build from source

Needs a .NET SDK. The project targets `net472` and compiles against the game's own assemblies,
which cannot be redistributed — **so there are no default paths and you must supply both:**

```bash
dotnet build -c Release \
  -p:ValheimManaged="<game>/valheim_Data/Managed" \
  -p:BepInExCore="<profile>/BepInEx/core"
```

`<game>` is your Valheim install directory. `<profile>` is a BepInEx install or a mod-manager
profile — under r2modman that is
`%APPDATA%/r2modmanPlus-local/Valheim/profiles/<ProfileName>`.

Output: `bin/Release/net472/ValDataDumper.dll`.

To avoid retyping the paths, create a `Directory.Build.props` beside the `.csproj`:

```xml
<Project><PropertyGroup>
  <ValheimManaged>...</ValheimManaged>
  <BepInExCore>...</BepInExCore>
</PropertyGroup></Project>
```

It is gitignored, because it holds paths specific to your machine.

> **Do not commit the game DLLs or any dump output to "fix the build."** The assemblies are Iron
> Gate's and are not redistributable; exported icons are their artwork. Both are gitignored on
> purpose. A build failure here means a path is wrong — the error message names which property.

## Credits and license

See [CREDITS.md](CREDITS.md) — in particular Jötunn, whose output format this reimplements, and
WackysDatabase, whose stats shape it mirrors. No code from either is included.

MIT, see [LICENSE](LICENSE).

Valheim is a trademark of Iron Gate AB. Unofficial fan work, not affiliated with or endorsed by
Iron Gate AB.
