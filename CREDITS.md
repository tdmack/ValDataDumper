# Credits

**No code from any of these projects is included here.** ValDataDumper is original code that
reimplements an output *grammar*; it does not vendor or fork anyone's source. These are courtesy
credits, and — more usefully — a statement of what the output is compatible with.

## Jötunn / Valheim-Modding — MIT

[Valheim-Modding/Jotunn](https://github.com/Valheim-Modding/Jotunn)

Jötunn's JotunnDoc defined the `item-list.md` / `recipe-list.md` / `piece-list.md` format that
this tool emits, and the idea of a documentation dump generated from the running game. **Being a
drop-in for JotunnDoc output is the entire point of this tool** — anything already parsing those
files should not need to change.

ValDataDumper is a narrow, standalone way to produce that output on demand — not a replacement for
Jötunn, which is a full mod framework and the reason most Valheim mods work at all.

## WackysDatabase — MIT

[Wacky-Mole/WackysDatabase](https://github.com/Wacky-Mole/WackysDatabase)

`stats-dump.json` mirrors WackysDatabase's `StatsDump` / `SlimmedItem` shape deliberately, key for
key, so that anything consuming a WackysDatabase-produced stats fixture can consume this one with
no change.

## BepInEx — LGPL-2.1

[BepInEx/BepInEx](https://github.com/BepInEx/BepInEx)

The plugin framework this loads under. It is referenced at build time and never redistributed —
install it yourself, or use a mod manager that does.

---

## Disclaimer

Valheim is a trademark of Iron Gate AB. This is unofficial fan work, not affiliated with or
endorsed by Iron Gate AB.

This repository contains **no game assets**: no Valheim or Unity assemblies, and no dump output.
The icons this tool exports are Iron Gate's artwork and the markdown it writes is extracted game
data — both are yours to generate locally from a copy of the game you own, and neither is
redistributed here.
