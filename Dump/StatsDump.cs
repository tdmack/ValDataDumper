using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ValDataDumper.Dump
{
    /// <summary>
    /// Emits `stats-dump.json` in WackysDatabase's `StatsDump` / `SlimmedItem` shape, so it is
    /// a drop-in replacement for a WackysDatabase-produced fixture — consumers need no change.
    ///
    /// **A documented superset since 0.6.0.** `SlimmedItem` shipped `armorPerLevel`,
    /// `damagePerLevel`, `blockPowerPerLevel` and `durabilityPerLevel` but omitted
    /// `deflectionForcePerLevel` and `scaleWeightByQuality`, which left parry force and weight
    /// uncomputable at any level the source did not already enumerate. Both are added here,
    /// beside their siblings rather than exiled to `item-extras.json`, because splitting
    /// per-level stats across two files to preserve a defunct tool's exact key set would cost
    /// more than the compatibility is worth. Additive only: no existing key moves or changes.
    /// (`m_scaleByQuality` is deliberately not dumped — it scales the mesh, not a stat.)
    ///
    /// Keys are sorted and every field is always written (numbers default 0, strings ""),
    /// matching the existing fixture so a re-dump diffs cleanly.
    /// </summary>
    internal static class StatsDump
    {
        /// <summary>The 11 damage keys, in the fixed order `DamageBlock` declares.</summary>
        private static string DamageJson(HitData.DamageTypes d, string indent)
        {
            var pairs = new[]
            {
                ("blunt", d.m_blunt), ("chop", d.m_chop), ("fire", d.m_fire),
                ("frost", d.m_frost), ("lightning", d.m_lightning), ("pickaxe", d.m_pickaxe),
                ("pierce", d.m_pierce), ("poison", d.m_poison), ("slash", d.m_slash),
                ("spirit", d.m_spirit), ("damage", d.m_damage),
            };
            var sb = new StringBuilder("{\n");
            for (int i = 0; i < pairs.Length; i++)
            {
                sb.Append(indent).Append("  ").Append(Text.JsonString(pairs[i].Item1)).Append(": ")
                  .Append(Text.Num(pairs[i].Item2)).Append(i < pairs.Length - 1 ? ",\n" : "\n");
            }
            return sb.Append(indent).Append('}').ToString();
        }

        /// <summary>
        /// One attack's multipliers and cost, or `null` when the item has none. `attackAnimation`
        /// is empty on a secondary attack the item doesn't really have (every item carries a
        /// default `Attack` object), so consumers treat an empty animation as "no such attack".
        /// </summary>
        private static string AttackJson(Attack a, string indent)
        {
            if (a == null) return "null";
            string inner = indent + "  ";
            return "{\n" +
                inner + "\"attackType\": " + Text.JsonString(a.m_attackType.ToString()) + ",\n" +
                inner + "\"attackAnimation\": " + Text.JsonString(a.m_attackAnimation ?? "") + ",\n" +
                inner + "\"damageMultiplier\": " + Text.Num(a.m_damageMultiplier) + ",\n" +
                inner + "\"staggerMultiplier\": " + Text.Num(a.m_staggerMultiplier) + ",\n" +
                inner + "\"forceMultiplier\": " + Text.Num(a.m_forceMultiplier) + ",\n" +
                inner + "\"attackStamina\": " + Text.Num(a.m_attackStamina) + ",\n" +
                inner + "\"attackEitr\": " + Text.Num(a.m_attackEitr) + "\n" +
                indent + "}";
        }

        /// <summary>`{ "primary": …, "secondary": … }` — see <see cref="AttackJson"/>.</summary>
        private static string AttacksJson(ItemDrop.ItemData.SharedData sd, string indent)
        {
            string inner = indent + "  ";
            return "{\n" +
                inner + "\"primary\": " + AttackJson(sd.m_attack, inner) + ",\n" +
                inner + "\"secondary\": " + AttackJson(sd.m_secondaryAttack, inner) + "\n" +
                indent + "}";
        }

        /// <summary>Serialize one item's shared data as a `SlimmedItem`.</summary>
        private static string SlimJson(ItemDrop.ItemData.SharedData sd, string indent)
        {
            string inner = indent + "  ";
            var sb = new StringBuilder("{\n");
            void Line(string key, string value, bool last = false)
            {
                sb.Append(inner).Append(Text.JsonString(key)).Append(": ").Append(value)
                  .Append(last ? "\n" : ",\n");
            }

            Line("weight", Text.Num(sd.m_weight));
            // Weight is not flat across levels: GetWeight multiplies by
            // (1 + (quality-1) * m_scaleWeightByQuality). Without this, a computed weight
            // above the level the catalog hard-codes is simply wrong.
            Line("scaleWeightByQuality", Text.Num(sd.m_scaleWeightByQuality));
            Line("nameToken", Text.JsonString(sd.m_name ?? ""));
            Line("itemType", Text.JsonString(sd.m_itemType.ToString()));
            Line("skillType", Text.JsonString(sd.m_skillType.ToString()));
            Line("maxQuality", sd.m_maxQuality.ToString());
            Line("damage", DamageJson(sd.m_damages, inner));
            Line("damagePerLevel", DamageJson(sd.m_damagesPerLevel, inner));
            Line("armor", Text.Num(sd.m_armor));
            Line("armorPerLevel", Text.Num(sd.m_armorPerLevel));
            Line("blockPower", Text.Num(sd.m_blockPower));
            Line("blockPowerPerLevel", Text.Num(sd.m_blockPowerPerLevel));
            Line("deflectionForce", Text.Num(sd.m_deflectionForce));
            // The one per-level field SlimmedItem omitted, though it shipped every other
            // sibling. GetDeflectionForce is base + (quality-1) * this, so parry force was
            // the single displayed stat that could not be computed for a level the source
            // data did not already enumerate.
            Line("deflectionForcePerLevel", Text.Num(sd.m_deflectionForcePerLevel));
            Line("timedBlockBonus", Text.Num(sd.m_timedBlockBonus));
            Line("durability", Text.Num(sd.m_maxDurability));
            Line("durabilityPerLevel", Text.Num(sd.m_durabilityPerLevel));
            Line("backstab", Text.Num(sd.m_backstabBonus));
            Line("knockback", Text.Num(sd.m_attackForce));
            Line("movementModifier", Text.Num(sd.m_movementModifier));
            Line("eitrRegen", Text.Num(sd.m_eitrRegenModifier));
            Line("seEquip", Text.JsonString(sd.m_equipStatusEffect != null ? sd.m_equipStatusEffect.name : ""));
            Line("seSetEquip", Text.JsonString(sd.m_setStatusEffect != null ? sd.m_setStatusEffect.name : ""));
            // 0.10.0: the primary and secondary attack. Stagger dealt per hit is
            // (blunt + slash + pierce + lightning) × m_damageMultiplier × m_staggerMultiplier
            // (Character.ApplyDamage → HitData.GetTotalStaggerDamage; Attack.ModifyDamage), and the
            // two multipliers live on each weapon's prefab, so stagger can't be computed without them.
            Line("attacks", AttacksJson(sd, inner));
            // 0.10.0: the item's own damage-taken modifiers while worn or held — the Wolf
            // Armor Chest's frost resistance, the Fenris set's… These are not status effects,
            // so status-effects.json can't carry them. Same shape as a status effect's `mods`.
            Line("damageModifiers", EffectDump.Mods(sd.m_damageModifiers));

            if (sd.m_food > 0f)
            {
                var food = new StringBuilder("{\n");
                food.Append(inner).Append("  \"health\": ").Append(Text.Num(sd.m_food)).Append(",\n");
                food.Append(inner).Append("  \"stamina\": ").Append(Text.Num(sd.m_foodStamina)).Append(",\n");
                food.Append(inner).Append("  \"eitr\": ").Append(Text.Num(sd.m_foodEitr)).Append(",\n");
                food.Append(inner).Append("  \"regen\": ").Append(Text.Num(sd.m_foodRegen)).Append(",\n");
                food.Append(inner).Append("  \"burnTime\": ").Append(Text.Num(sd.m_foodBurnTime)).Append(",\n");
                food.Append(inner).Append("  \"isDrink\": ").Append(IsDrink(sd) ? "true" : "false").Append("\n");
                food.Append(inner).Append('}');
                Line("food", food.ToString(), true);
            }
            else
            {
                Line("food", "null", true);
            }

            return sb.Append(indent).Append('}').ToString();
        }

        /// <summary>
        /// Drinks are consumables the game animates as drinking. 1.0 keeps the flag on
        /// SharedData; the reflection fallback keeps a rename from breaking the whole dump.
        /// </summary>
        private static bool IsDrink(ItemDrop.ItemData.SharedData sd)
        {
            var field = typeof(ItemDrop.ItemData.SharedData).GetField("m_isDrink");
            if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(sd);
            return false;
        }

        /// <summary>Write `stats-dump.json` for every item, keyed by prefab, keys sorted.</summary>
        public static int Write(List<KeyValuePair<string, ItemDrop>> items, string version, string path)
        {
            var sorted = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var kv in items)
            {
                sorted[kv.Key] = SlimJson(kv.Value.m_itemData.m_shared, "    ");
            }

            var sb = new StringBuilder();
            sb.Append("{\n  \"pinnedGameVersion\": ").Append(Text.JsonString(version)).Append(",\n  \"items\": {\n");
            int i = 0;
            foreach (var kv in sorted)
            {
                sb.Append("    ").Append(Text.JsonString(kv.Key)).Append(": ").Append(kv.Value)
                  .Append(++i < sorted.Count ? ",\n" : "\n");
            }
            sb.Append("  }\n}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return sorted.Count;
        }
    }
}
