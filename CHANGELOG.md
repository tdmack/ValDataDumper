# Changelog

Versions track the plugin, not the game. The Valheim version a dump came from is recorded in
`manifest.json` and stamped into every generated markdown file.

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
