using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using UnityEngine;

namespace ValDataDumper.Dump
{
    /// <summary>Counts reported after a dump.</summary>
    internal sealed class DumpStats
    {
        public int Items, Recipes, PieceTables, Pieces, Icons, IconFailures, Tokens, Stats, PieceExtras, ItemExtras, StatusEffects;
    }

    /// <summary>
    /// Walks the live <see cref="ObjectDB"/> and every <see cref="PieceTable"/> reachable from it
    /// and writes Jötunn-format fixtures (the exact column/cell grammar JotunnDoc emits,
    /// so existing parsers need no change), icons, recipe stations and a localization table.
    /// </summary>
    internal static class Dumper
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static DumpStats Run(string outDir, string version, bool exportIcons)
        {
            var db = ObjectDB.instance;
            if (db == null) throw new InvalidOperationException("ObjectDB.instance is null — not in-world yet");

            var stats = new DumpStats();
            var loc = new LocTable();

            string objectsDir = Path.Combine(outDir, "data", "objects");
            string piecesDir = Path.Combine(outDir, "data", "pieces");
            string itemImages = Path.Combine(outDir, "images", "items");
            string pieceImages = Path.Combine(outDir, "images", "pieces");
            Directory.CreateDirectory(objectsDir);
            Directory.CreateDirectory(piecesDir);

            var items = CollectItems(db);
            WriteItems(items, version, exportIcons, itemImages, Path.Combine(objectsDir, "item-list.md"), loc, stats);
            WriteRecipes(db, version, Path.Combine(objectsDir, "recipe-list.md"), loc, stats);
            WritePieces(items, version, exportIcons, pieceImages, Path.Combine(piecesDir, "piece-list.md"), loc, stats);
            WriteStations(db, version, Path.Combine(outDir, "recipe-stations.json"));

            // Facets the markdown format has no column for (see StatsDump / ExtraDump).
            stats.Stats = StatsDump.Write(items, version, Path.Combine(outDir, "stats-dump.json"));
            stats.ItemExtras = ExtraDump.WriteItems(items, version, Path.Combine(outDir, "item-extras.json"));
            stats.StatusEffects = EffectDump.Write(items, version, Path.Combine(outDir, "status-effects.json"));
            stats.PieceExtras = ExtraDump.WritePieces(AllPieceObjects(items), version, Path.Combine(outDir, "piece-extras.json"));
            stats.Tokens = loc.Write(Path.Combine(outDir, "localization.json"));
            WriteManifest(version, stats, Path.Combine(outDir, "manifest.json"));
            return stats;
        }

        // ------------------------------------------------------------------ items

        /// <summary>
        /// Every item in the game: <see cref="ObjectDB.m_items"/> unioned with every recipe's
        /// crafted item.
        ///
        /// The union is a belt-and-braces guard so a recipe's output can never be missing from
        /// the item list. As of 1.0.7 it adds nothing — every `Recipe.m_item` is already in
        /// `m_items`. (It was added on the mistaken belief that ~77 craftables were missing;
        /// that came from comparing recipe names against item prefab names, which legitimately
        /// differ — `Recipe_SwordKrom` crafts the prefab `THSwordKrom`. Kept because it costs
        /// one dictionary pass and would catch a real omission if the game ever introduced one.)
        /// </summary>
        private static List<KeyValuePair<string, ItemDrop>> CollectItems(ObjectDB db)
        {
            var byPrefab = new Dictionary<string, ItemDrop>();

            void Add(GameObject go)
            {
                if (go == null || byPrefab.ContainsKey(go.name)) return;
                var drop = go.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return;
                byPrefab[go.name] = drop;
            }

            foreach (var go in db.m_items) Add(go);
            foreach (var recipe in db.m_recipes)
            {
                if (recipe != null && recipe.m_item != null) Add(recipe.m_item.gameObject);
            }

            var list = new List<KeyValuePair<string, ItemDrop>>(byPrefab);
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }

        private static void WriteItems(
            List<KeyValuePair<string, ItemDrop>> items, string version, bool exportIcons,
            string imagesDir, string path, LocTable loc, DumpStats stats)
        {
            var sb = new StringBuilder();
            sb.Append("# Item list\n");
            sb.Append("All of the items currently in the game, with English localizations applied\n");
            sb.Append(Stamp(version));
            sb.Append("\n|Item |AssetID |Token |English Name |Type |Description |\n");
            sb.Append("|---|---|---|---|---|---|\n");

            foreach (var kv in items)
            {
                string prefab = kv.Key;
                var sd = kv.Value.m_itemData.m_shared;

                string itemCell = prefab;
                if (exportIcons && sd.m_icons != null && sd.m_icons.Length > 0 && sd.m_icons[0] != null)
                {
                    if (IconExport.Write(sd.m_icons[0], Path.Combine(imagesDir, prefab + ".png")))
                    {
                        stats.Icons++;
                        itemCell += "<br><img src=\"../../images/items/" + prefab + ".png\">";
                    }
                    else stats.IconFailures++;
                }

                sb.Append('|').Append(itemCell)
                  .Append('|').Append(Text.PseudoAssetId(prefab))
                  .Append('|').Append(Text.Cell(sd.m_name))
                  .Append('|').Append(Text.CellOrNull(loc.Resolve(sd.m_name)))
                  .Append('|').Append(sd.m_itemType.ToString())
                  .Append('|').Append(Text.CellOrNull(loc.Resolve(sd.m_description)))
                  .Append("|\n");
                stats.Items++;
            }

            File.WriteAllText(path, sb.ToString(), Utf8NoBom);
        }

        // ---------------------------------------------------------------- recipes

        private static void WriteRecipes(ObjectDB db, string version, string path, LocTable loc, DumpStats stats)
        {
            var recipes = db.m_recipes.Where(r => r != null).OrderBy(r => r.name, StringComparer.Ordinal).ToList();

            var sb = new StringBuilder();
            sb.Append("# Recipe list\n");
            sb.Append("All the recipes currently in the game, with English localizations applied.\n");
            sb.Append(Stamp(version));
            sb.Append("\n|Name |AssetID |Item name |Amount |Resources required |\n");
            sb.Append("|---|---|---|---|---|\n");

            foreach (var r in recipes)
            {
                var shared = r.m_item != null && r.m_item.m_itemData != null ? r.m_item.m_itemData.m_shared : null;
                string itemName = shared != null ? Text.CellOrNull(loc.Resolve(shared.m_name)) : Text.Null;
                int maxQuality = shared != null ? Math.Max(1, shared.m_maxQuality) : 1;

                sb.Append('|').Append(r.name)
                  .Append('|').Append(Text.PseudoAssetId(r.name))
                  .Append('|').Append(itemName)
                  .Append('|').Append(r.m_amount)
                  .Append('|').Append(ResourceCell(r.m_resources, maxQuality, loc))
                  .Append("|\n");
                stats.Recipes++;
            }

            File.WriteAllText(path, sb.ToString(), Utf8NoBom);
        }

        /// <summary>
        /// Jötunn's two grammars: single-level <c>&lt;ul&gt;…&lt;/ul&gt;</c>, or, for upgradable
        /// items with any per-level cost, <c>Level N:&lt;ul&gt;…&lt;/ul&gt;</c> per level where level
        /// N ≥ 2 lists the *single-upgrade* cost (<see cref="Piece.Requirement.GetAmount"/>).
        /// Levels that would list nothing are omitted (the parser rejects empty levels).
        /// </summary>
        private static string ResourceCell(Piece.Requirement[] requirements, int maxQuality, LocTable loc)
        {
            var reqs = (requirements ?? new Piece.Requirement[0]).Where(q => q != null && q.m_resItem != null).ToList();
            bool multi = maxQuality > 1 && reqs.Any(q => q.m_amountPerLevel > 0);

            if (!multi)
            {
                return Text.ResourceList(LevelItems(reqs, 1, loc));
            }

            var sb = new StringBuilder();
            for (int level = 1; level <= maxQuality; level++)
            {
                var items = LevelItems(reqs, level, loc);
                if (items.Count == 0) continue;
                sb.Append("Level ").Append(level).Append(':').Append(Text.ResourceList(items));
            }
            return sb.ToString();
        }

        private static List<KeyValuePair<string, int>> LevelItems(List<Piece.Requirement> reqs, int level, LocTable loc)
        {
            var list = new List<KeyValuePair<string, int>>();
            foreach (var q in reqs)
            {
                int amount = q.GetAmount(level);
                if (amount <= 0) continue;
                list.Add(new KeyValuePair<string, int>(loc.Resolve(q.m_resItem.m_itemData.m_shared.m_name), amount));
            }
            return list;
        }

        // ----------------------------------------------------------------- pieces

        private static void WritePieces(
            List<KeyValuePair<string, ItemDrop>> items, string version, bool exportIcons,
            string imagesDir, string path, LocTable loc, DumpStats stats)
        {
            var tables = new SortedDictionary<string, PieceTable>(StringComparer.Ordinal);
            foreach (var kv in items)
            {
                var pt = kv.Value.m_itemData.m_shared.m_buildPieces;
                if (pt != null && !tables.ContainsKey(pt.name)) tables[pt.name] = pt;
            }

            var sb = new StringBuilder();
            sb.Append("# Piece list\n");
            sb.Append("All of the pieces currently in the game.\n");
            sb.Append(Stamp(version));

            foreach (var entry in tables)
            {
                sb.Append("## ").Append(entry.Key).Append("\n\n");
                sb.Append("|Piece |AssetID |Token |English Name |Description |Resources required |Material Type |\n");
                sb.Append("|---|---|---|---|---|---|---|\n");

                foreach (var pgo in entry.Value.m_pieces)
                {
                    if (pgo == null) continue;
                    var piece = pgo.GetComponent<Piece>();
                    if (piece == null) continue;
                    string prefab = pgo.name;

                    string pieceCell = prefab;
                    if (exportIcons && piece.m_icon != null)
                    {
                        if (IconExport.Write(piece.m_icon, Path.Combine(imagesDir, prefab + ".png")))
                        {
                            stats.Icons++;
                            pieceCell += "<br><img src=\"../../images/pieces/" + prefab + ".png\">";
                        }
                        else stats.IconFailures++;
                    }

                    var resources = (piece.m_resources ?? new Piece.Requirement[0])
                        .Where(q => q != null && q.m_resItem != null && q.m_amount > 0)
                        .Select(q => new KeyValuePair<string, int>(loc.Resolve(q.m_resItem.m_itemData.m_shared.m_name), q.m_amount))
                        .ToList();

                    var wearNTear = pgo.GetComponent<WearNTear>();
                    string materialType = wearNTear != null ? wearNTear.m_materialType.ToString() : "";

                    sb.Append('|').Append(pieceCell)
                      .Append('|').Append(Text.PseudoAssetId(prefab))
                      .Append('|').Append(Text.Cell(piece.m_name))
                      .Append('|').Append(Text.CellOrNull(loc.Resolve(piece.m_name)))
                      .Append('|').Append(Text.CellOrNull(loc.Resolve(piece.m_description)))
                      .Append('|').Append(Text.ResourceList(resources))
                      .Append('|').Append(materialType)
                      .Append("|\n");
                    stats.Pieces++;
                }
                sb.Append('\n');
                stats.PieceTables++;
            }

            File.WriteAllText(path, sb.ToString(), Utf8NoBom);
        }

        /// <summary>
        /// Piece GameObjects from the **placeable** build tables only.
        ///
        /// The `_FeasterPieceTable` is excluded: its entries are food items placed as a feast,
        /// so they carry a Piece component with a default comfort of 1 and would otherwise show
        /// up as furniture (every mead reported `comfort=1, group=Chair`). Mirrors
        /// `IMPORTABLE_PIECE_TABLES` in `buildImportedPieces.ts`.
        /// </summary>
        private static readonly HashSet<string> PlaceableTables = new HashSet<string>
        {
            "_HammerPieceTable",
            "_CultivatorPieceTable",
        };

        private static IEnumerable<GameObject> AllPieceObjects(List<KeyValuePair<string, ItemDrop>> items)
        {
            var tables = new HashSet<string>();
            foreach (var kv in items)
            {
                var pt = kv.Value.m_itemData.m_shared.m_buildPieces;
                if (pt == null || !PlaceableTables.Contains(pt.name) || !tables.Add(pt.name)) continue;
                foreach (var go in pt.m_pieces) if (go != null) yield return go;
            }
        }

        // --------------------------------------------------------------- stations

        /// <summary>
        /// Matches `RecipeStationDump` (`fixtures/recipe-stations.json`), keys sorted.
        ///
        /// `noCraftOnlyUpgrade` is the game's "upgrade-only" flag: the recipe is hidden from the
        /// craft list (`InventoryGui` skips it when listing new crafts) but still drives upgrades.
        /// Valheim 1.0 sets it on every finished Nord item, whose level 1 comes from hardening a
        /// Cast in the Frost Foundry instead. Patch 1.0.14 fixed a missing flag on Helmet of the
        /// Protector, which is exactly the kind of drift this field makes visible.
        ///
        /// `requireOnlyOneIngredient` marks a recipe that takes **any one** of its listed
        /// resources, not all of them: the craft consumes the first one the player holds. Raw Fish
        /// (`Recipe_Fish1`) works this way, listing every fish, so reading its resources as a sum
        /// charges one of each. For these recipes the output grows with the ingredient's quality:
        /// `amount + ceil((quality − 1) × amount × qualityResultAmountMultiplier)` (the game's
        /// `Recipe.GetAmount`). The multiplier is dumped for every recipe but only matters here.
        /// </summary>
        private static void WriteStations(ObjectDB db, string version, string path)
        {
            var byPrefab = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var r in db.m_recipes)
            {
                if (r == null) continue;
                string prefab = r.name.StartsWith("Recipe_") ? r.name.Substring("Recipe_".Length) : r.name;
                string station = r.m_craftingStation != null ? r.m_craftingStation.m_name : "";
                byPrefab[prefab] =
                    "    " + Text.JsonString(prefab) + ": {\n" +
                    "      \"prefab\": " + Text.JsonString(prefab) + ",\n" +
                    "      \"craftingStation\": " + Text.JsonString(station) + ",\n" +
                    "      \"minStationLevel\": " + r.m_minStationLevel + ",\n" +
                    "      \"noCraftOnlyUpgrade\": " + (r.m_noCraftOnlyUpgrade ? "true" : "false") + ",\n" +
                    "      \"requireOnlyOneIngredient\": " + (r.m_requireOnlyOneIngredient ? "true" : "false") + ",\n" +
                    "      \"qualityResultAmountMultiplier\": " + Text.Num(r.m_qualityResultAmountMultiplier) + "\n" +
                    "    }";
            }

            var sb = new StringBuilder();
            sb.Append("{\n  \"pinnedGameVersion\": ").Append(Text.JsonString(version)).Append(",\n  \"recipes\": {\n");
            sb.Append(string.Join(",\n", byPrefab.Values));
            sb.Append("\n  }\n}\n");
            File.WriteAllText(path, sb.ToString(), Utf8NoBom);
        }

        // --------------------------------------------------------------- manifest

        private static void WriteManifest(string version, DumpStats s, string path)
        {
            string json =
                "{\n" +
                "  \"valheimVersion\": " + Text.JsonString(version) + ",\n" +
                "  \"generatedAt\": " + Text.JsonString(DateTime.UtcNow.ToString("o")) + ",\n" +
                "  \"plugin\": " + Text.JsonString(Plugin.Name + " " + Plugin.PluginVersion) + ",\n" +
                "  \"items\": " + s.Items + ",\n" +
                "  \"recipes\": " + s.Recipes + ",\n" +
                "  \"pieceTables\": " + s.PieceTables + ",\n" +
                "  \"pieces\": " + s.Pieces + ",\n" +
                "  \"icons\": " + s.Icons + ",\n" +
                "  \"iconFailures\": " + s.IconFailures + ",\n" +
                "  \"stats\": " + s.Stats + ",\n" +
                "  \"itemExtras\": " + s.ItemExtras + ",\n" +
                "  \"pieceExtras\": " + s.PieceExtras + ",\n" +
                "  \"statusEffects\": " + s.StatusEffects + "\n" +
                "}\n";
            File.WriteAllText(path, json, Utf8NoBom);
        }

        /// <summary>The header line `extractDataVersion` reads (`Valheim X.Y.Z`).</summary>
        private static string Stamp(string version) =>
            "This file is automatically generated from Valheim " + version +
            " using the ValDataDumper plugin (JotunnDoc-compatible format).\n";
    }

    /// <summary>Localizes tokens and remembers every `$token` → English pair it resolved.</summary>
    internal sealed class LocTable
    {
        private readonly SortedDictionary<string, string> _seen = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public string Resolve(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string english = Localization.instance != null ? Localization.instance.Localize(raw) : raw;
            if (raw.StartsWith("$")) _seen[raw] = english;
            return english;
        }

        public int Write(string path)
        {
            var sb = new StringBuilder("{\n");
            sb.Append(string.Join(",\n", _seen.Select(kv => "  " + Text.JsonString(kv.Key) + ": " + Text.JsonString(kv.Value))));
            sb.Append("\n}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return _seen.Count;
        }
    }
}
