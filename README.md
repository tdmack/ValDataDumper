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
[Info   :ValDataDumper] ValDataDumper 0.5.0 loaded; output → ...\BepInEx\config\valdatadumper
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
recipe-stations.json          prefab -> crafting-station token + minimum station level
stats-dump.json               per-item stats in WackysDatabase's SlimmedItem shape
item-extras.json              stack size, teleportability, vendor value, tool tier, set effects
piece-extras.json             comfort, container size, build station
localization.json             every $token encountered -> English
manifest.json                 game version, timestamp, counts
```

Every generated markdown file carries a header stamp naming the game version it came from, and
`manifest.json` records the same version — so a half-finished or mismatched dump is detectable.

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

`stats-dump.json` matches WackysDatabase's `StatsDump` / `SlimmedItem` shape key for key, so
consumers of that fixture need no change.

**Tested against Valheim 1.0.7 with BepInEx 5.4.23.5.** It reads only public game state, so it is
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
