using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;

namespace ValDataDumper.Dump
{
    /// <summary>
    /// Facets the Jötunn markdown format has no column for, emitted as side files.
    ///
    /// `piece-extras.json` covers comfort, container storage, the piece's build station, and —
    /// for converters — the `Smelter` configuration (Smelter, Blast Furnace, Charcoal Kiln,
    /// Frigid Kiln, …), the `CookingStation` configuration (cooking racks, Frost Foundry, …) and
    /// the `Fermenter` configuration (mead base → meads): conversions, fuel, yields, and timing.
    /// These facets are otherwise hand-curated from the wiki, one row at a time.
    ///
    /// It deliberately carries **no size**. Published piece sizes are hand-entered from the
    /// wiki with no consistent axis convention — "Wood Floor 2x2" is its x/z footprint, "Wood
    /// Door 2x2" its x/y elevation, and "Darkwood Arch 4x1" matches no axis of a
    /// 2.46 x 2.23 x 0.57 mesh — so it is not a function of game geometry and no read
    /// reproduces it. Mesh bounds and snap points were both tried; each scored 0/43 against
    /// the curated values, so the field was dropped entirely rather than left half-populated
    /// and ambiguous. The wiki owns piece dimensions; this tool does not guess at them.
    ///
    /// `item-extras.json` covers facets the markdown format has no column for at all: stack
    /// size, teleportability (which ores can't go through a portal — real planning value),
    /// vendor value, tool tier, the set/status-effect wiring, and the **Upgrader (Refinement
    /// Forge)** block — what a refinement attempt at the Forge of Potential actually rolls.
    ///
    /// Those four live here rather than in `stats-dump.json` because that file deliberately
    /// mirrors WackysDatabase's `SlimmedItem` shape and should stay a drop-in for it; this
    /// file is ours to extend.
    /// </summary>
    internal static class ExtraDump
    {
        /// <summary>Localize a token, tolerating a null Localization (returns the raw token).</summary>
        private static string Loc(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }

        /// <summary>Write `piece-extras.json`, keyed by piece prefab.</summary>
        public static int WritePieces(IEnumerable<GameObject> pieces, string version, string path)
        {
            var rows = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var go in pieces)
            {
                if (go == null) continue;
                var piece = go.GetComponent<Piece>();
                if (piece == null || rows.ContainsKey(go.name)) continue;

                var sb = new StringBuilder("{\n");
                sb.Append("      \"name\": ").Append(Text.JsonString(Localization.instance != null
                    ? Localization.instance.Localize(piece.m_name) : piece.m_name)).Append(",\n");
                sb.Append("      \"comfort\": ").Append(piece.m_comfort).Append(",\n");
                sb.Append("      \"comfortGroup\": ").Append(Text.JsonString(piece.m_comfortGroup.ToString())).Append(",\n");

                var container = go.GetComponent<Container>();
                sb.Append("      \"storage\": ").Append(container != null
                    ? "{ \"width\": " + container.m_width + ", \"height\": " + container.m_height + " }"
                    : "null").Append(",\n");

                sb.Append("      \"station\": ").Append(Text.JsonString(piece.m_craftingStation != null
                    ? piece.m_craftingStation.m_name : "")).Append(",\n");

                // `GetComponentInChildren(true)` includes the root and inactive children. Don't
                // use `??` between Unity lookups: UnityEngine.Object overrides ==, not ??.
                var smelter = go.GetComponentInChildren<Smelter>(true);
                sb.Append("      \"smelter\": ").Append(smelter != null ? SmelterJson(smelter) : "null")
                  .Append(",\n");

                var cooking = go.GetComponentInChildren<CookingStation>(true);
                sb.Append("      \"cookingStation\": ").Append(cooking != null ? CookingStationJson(cooking) : "null")
                  .Append(",\n");

                var fermenter = go.GetComponentInChildren<Fermenter>(true);
                sb.Append("      \"fermenter\": ").Append(fermenter != null ? FermenterJson(fermenter) : "null")
                  .Append("\n    }");
                rows[go.name] = sb.ToString();
            }
            Write(path, version, "pieces", rows);
            return rows.Count;
        }

        /// <summary>
        /// A `Smelter` component's configuration: what it converts, what it burns, and how fast.
        ///
        /// Every conversion is one `from` → one `to`. Each product burns `fuelPerProduct` units of
        /// `fuelItem` (fuel drains at `fuelPerProduct / secPerProduct` per second), and `fuelItem`
        /// is null for converters with no fuel (Charcoal Kiln, Windmill, Spinning Wheel). All
        /// items are prefab names, joinable to `item-list.md`.
        ///
        /// A conversion's `from` is **null** for a no-source converter — the game's
        /// `m_noSourceConversion`, set in `Awake` whenever a conversion has no input — which
        /// turns fuel alone into output. Valheim 1.0's Frigid Kiln works this way: Ice is its
        /// fuel and Liquid Frost (`FrozenFuel`) its product. Those numbers exist only on the
        /// prefab — the in-code initializers (`m_fuelPerProduct = 4`, `m_secPerProduct = 10`) are
        /// not the game's values — so a live dump is the only honest source for them.
        /// </summary>
        private static string SmelterJson(Smelter s)
        {
            var sb = new StringBuilder("{\n");
            sb.Append("        \"fuelItem\": ").Append(s.m_fuelItem != null
                ? Text.JsonString(s.m_fuelItem.gameObject.name) : "null").Append(",\n");
            sb.Append("        \"fuelPerProduct\": ").Append(s.m_fuelPerProduct).Append(",\n");
            sb.Append("        \"secPerProduct\": ").Append(Text.Num(s.m_secPerProduct)).Append(",\n");
            sb.Append("        \"maxOre\": ").Append(s.m_maxOre).Append(",\n");
            sb.Append("        \"maxFuel\": ").Append(s.m_maxFuel).Append(",\n");
            sb.Append("        \"conversions\": [");
            bool first = true;
            foreach (var c in s.m_conversion)
            {
                if (c == null || c.m_to == null) continue;
                sb.Append(first ? "\n" : ",\n");
                sb.Append("          { \"from\": ").Append(c.m_from != null
                    ? Text.JsonString(c.m_from.gameObject.name) : "null")
                  .Append(", \"to\": ").Append(Text.JsonString(c.m_to.gameObject.name)).Append(" }");
                first = false;
            }
            sb.Append(first ? "]\n" : "\n        ]\n");
            sb.Append("      }");
            return sb.ToString();
        }

        /// <summary>
        /// A `CookingStation` component's configuration: what each slot turns into what, how long
        /// it takes, and — for fuelled stations — what it burns.
        ///
        /// Unlike a `Smelter`, fuel is burned **by time**, not per product: while the station is
        /// working it drains `1 / secPerFuel` units per second, shared by every occupied slot.
        /// When `useFuelWhileEmpty` is true it keeps burning with nothing inside. Stations with
        /// `requireFire` need a lit fire beneath instead (cooking racks). `slots` is how many
        /// items cook at once.
        ///
        /// In Valheim 1.0 this is how the Frost Foundry hardens a Cast (`…Uncooked`) into its
        /// finished Nord item, burning Liquid Frost (`FrozenFuel`) — so a single Cast costs
        /// `cookTime / secPerFuel` Liquid Frost when it cooks alone.
        ///
        /// `useFuelWhileEmpty` corrects the game's field spelling (`m_useFueldWhileEmpty`).
        /// </summary>
        private static string CookingStationJson(CookingStation c)
        {
            var sb = new StringBuilder("{\n");
            sb.Append("        \"useFuel\": ").Append(c.m_useFuel ? "true" : "false").Append(",\n");
            sb.Append("        \"fuelItem\": ").Append(c.m_fuelItem != null
                ? Text.JsonString(c.m_fuelItem.gameObject.name) : "null").Append(",\n");
            sb.Append("        \"secPerFuel\": ").Append(c.m_secPerFuel).Append(",\n");
            sb.Append("        \"maxFuel\": ").Append(c.m_maxFuel).Append(",\n");
            sb.Append("        \"useFuelWhileEmpty\": ").Append(c.m_useFueldWhileEmpty ? "true" : "false").Append(",\n");
            sb.Append("        \"requireFire\": ").Append(c.m_requireFire ? "true" : "false").Append(",\n");
            sb.Append("        \"slots\": ").Append(c.m_slots != null ? c.m_slots.Length : 0).Append(",\n");
            sb.Append("        \"conversions\": [");
            bool first = true;
            foreach (var conv in c.m_conversion)
            {
                if (conv == null || conv.m_from == null || conv.m_to == null) continue;
                sb.Append(first ? "\n" : ",\n");
                sb.Append("          { \"from\": ").Append(Text.JsonString(conv.m_from.gameObject.name))
                  .Append(", \"to\": ").Append(Text.JsonString(conv.m_to.gameObject.name))
                  .Append(", \"cookTime\": ").Append(Text.Num(conv.m_cookTime)).Append(" }");
                first = false;
            }
            sb.Append(first ? "]\n" : "\n        ]\n");
            sb.Append("      }");
            return sb.ToString();
        }

        /// <summary>
        /// A `Fermenter` component's configuration: which mead base becomes which mead, how many
        /// a batch yields, and how long it takes.
        ///
        /// A fermenter holds one item at a time and burns no fuel. After `fermentationDuration`
        /// seconds, tapping it drops `producedItems` of `to` for the single `from` put in. Both
        /// numbers exist only on the prefab — the in-code initializers (`m_producedItems = 4`,
        /// `m_fermentationDuration = 2400`) are not the game's values — so a live dump is the only
        /// honest source for them. Items are prefab names, joinable to `item-list.md`.
        /// </summary>
        private static string FermenterJson(Fermenter f)
        {
            var sb = new StringBuilder("{\n");
            sb.Append("        \"fermentationDuration\": ").Append(Text.Num(f.m_fermentationDuration)).Append(",\n");
            sb.Append("        \"conversions\": [");
            bool first = true;
            foreach (var conv in f.m_conversion)
            {
                if (conv == null || conv.m_from == null || conv.m_to == null) continue;
                sb.Append(first ? "\n" : ",\n");
                sb.Append("          { \"from\": ").Append(Text.JsonString(conv.m_from.gameObject.name))
                  .Append(", \"to\": ").Append(Text.JsonString(conv.m_to.gameObject.name))
                  .Append(", \"producedItems\": ").Append(conv.m_producedItems).Append(" }");
                first = false;
            }
            sb.Append(first ? "]\n" : "\n        ]\n");
            sb.Append("      }");
            return sb.ToString();
        }

        /// <summary>Write `item-extras.json`, keyed by item prefab.</summary>
        public static int WriteItems(List<KeyValuePair<string, ItemDrop>> items, string version, string path)
        {
            var rows = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var kv in items)
            {
                var sd = kv.Value.m_itemData.m_shared;
                var sb = new StringBuilder("{\n");
                sb.Append("      \"maxStackSize\": ").Append(sd.m_maxStackSize).Append(",\n");
                sb.Append("      \"teleportable\": ").Append(sd.m_teleportable ? "true" : "false").Append(",\n");
                sb.Append("      \"value\": ").Append(sd.m_value).Append(",\n");
                sb.Append("      \"toolTier\": ").Append(sd.m_toolTier).Append(",\n");
                sb.Append("      \"setName\": ").Append(Text.JsonString(sd.m_setName ?? "")).Append(",\n");
                sb.Append("      \"setSize\": ").Append(sd.m_setSize).Append(",\n");
                sb.Append("      \"setStatusEffect\": ").Append(Text.JsonString(
                    sd.m_setStatusEffect != null ? sd.m_setStatusEffect.name : "")).Append(",\n");
                // The human-readable set bonus, which otherwise has to be transcribed by hand.
                sb.Append("      \"setEffectName\": ").Append(Text.JsonString(
                    sd.m_setStatusEffect != null ? Loc(sd.m_setStatusEffect.m_name) : "")).Append(",\n");
                sb.Append("      \"setEffectTooltip\": ").Append(Text.JsonString(
                    sd.m_setStatusEffect != null ? Loc(sd.m_setStatusEffect.m_tooltip) : "")).Append(",\n");
                sb.Append("      \"equipEffectTooltip\": ").Append(Text.JsonString(
                    sd.m_equipStatusEffect != null ? Loc(sd.m_equipStatusEffect.m_tooltip) : "")).Append(",\n");
                sb.Append("      \"equipStatusEffect\": ").Append(Text.JsonString(
                    sd.m_equipStatusEffect != null ? sd.m_equipStatusEffect.name : "")).Append(",\n");
                sb.Append("      \"consumeStatusEffect\": ").Append(Text.JsonString(
                    sd.m_consumeStatusEffect != null ? sd.m_consumeStatusEffect.name : "")).Append(",\n");

                // The Upgrader (Refinement Forge) block — the game's own grouping for these four,
                // and the whole of what a refinement attempt rolls: a success raises the item by
                // `successUpgradeSteps`, a break destroys it and refunds
                // `breakReturnIngredientsAmount` of the recoverable requirements, and anything
                // else drops it a level.
                //
                // They are emitted for every item, not just idols, because the fields sit on every
                // SharedData — a consumer should not have to guess which rows carry them.
                //
                // Worth dumping precisely because their in-code initializers (0.65 / 0.1 / 1 / 0.5)
                // are per-prefab overridable: reading a live ObjectDB is the only honest way to
                // know a given idol's real odds.
                sb.Append("      \"upgradeChance\": ").Append(Text.Num(sd.m_upgradeChance)).Append(",\n");
                sb.Append("      \"breakChance\": ").Append(Text.Num(sd.m_breakChance)).Append(",\n");
                sb.Append("      \"successUpgradeSteps\": ").Append(sd.m_successUpgradeSteps).Append(",\n");
                // Key spelling corrected; the game's field is `m_breakReturnIngreientsAmount`.
                sb.Append("      \"breakReturnIngredientsAmount\": ")
                  .Append(Text.Num(sd.m_breakReturnIngreientsAmount)).Append("\n    }");
                rows[kv.Key] = sb.ToString();
            }
            Write(path, version, "items", rows);
            return rows.Count;
        }

        private static void Write(string path, string version, string key, SortedDictionary<string, string> rows)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"pinnedGameVersion\": ").Append(Text.JsonString(version))
              .Append(",\n  \"").Append(key).Append("\": {\n");
            int i = 0;
            foreach (var kv in rows)
            {
                sb.Append("    ").Append(Text.JsonString(kv.Key)).Append(": ").Append(kv.Value)
                  .Append(++i < rows.Count ? ",\n" : "\n");
            }
            sb.Append("  }\n}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
