using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace ValDataDumper.Dump
{
    /// <summary>
    /// `status-effects.json`: every <see cref="StatusEffect"/> an item refers to — its set bonus
    /// (`m_setStatusEffect`), equip effect and consume effect (meads, food) — keyed by the
    /// effect's prefab name, which is what `item-extras.json` already names. Each row carries the
    /// localized name and tooltip, the duration, and, for <see cref="SE_Stats"/>, the modifiers
    /// that describe what it does. Values are raw game fields: a multiplier of 1 or a modifier of
    /// 0 means "no change"; `mods` are damage-taken modifiers (Resistant, SlightlyWeak, …).
    /// </summary>
    internal static class EffectDump
    {
        /// <summary>Write `status-effects.json`; returns the number of effects written.</summary>
        public static int Write(List<KeyValuePair<string, ItemDrop>> items, string version, string path)
        {
            var effects = new SortedDictionary<string, StatusEffect>(StringComparer.Ordinal);
            foreach (var kv in items)
            {
                var sd = kv.Value.m_itemData.m_shared;
                Add(effects, sd.m_setStatusEffect);
                Add(effects, sd.m_equipStatusEffect);
                Add(effects, sd.m_consumeStatusEffect);
            }
            var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in effects) rows[kv.Key] = Row(kv.Value);
            ExtraDump.Write(path, version, "statusEffects", rows);
            return rows.Count;
        }

        private static void Add(SortedDictionary<string, StatusEffect> into, StatusEffect se)
        {
            // Unity-null check: don't use `?.` on UnityEngine.Object.
            if (se == null || into.ContainsKey(se.name)) return;
            into[se.name] = se;
        }

        private static string Row(StatusEffect se)
        {
            var sb = new StringBuilder("{\n");
            sb.Append("      \"name\": ").Append(Text.JsonString(ExtraDump.Loc(se.m_name))).Append(",\n");
            sb.Append("      \"tooltip\": ").Append(Text.JsonString(ExtraDump.Loc(se.m_tooltip))).Append(",\n");
            sb.Append("      \"type\": ").Append(Text.JsonString(se.GetType().Name)).Append(",\n");
            sb.Append("      \"ttl\": ").Append(Text.Num(se.m_ttl)).Append(",\n");
            var s = se as SE_Stats;
            sb.Append("      \"stats\": ").Append(s != null ? Stats(s) : "null").Append("\n    }");
            return sb.ToString();
        }

        private static string Stats(SE_Stats s)
        {
            var f = new List<string>
            {
                N("runStaminaDrainModifier", s.m_runStaminaDrainModifier),
                N("runStaminaUseModifier", s.m_runStaminaUseModifier),
                N("jumpStaminaUseModifier", s.m_jumpStaminaUseModifier),
                N("attackStaminaUseModifier", s.m_attackStaminaUseModifier),
                N("blockStaminaUseModifier", s.m_blockStaminaUseModifier),
                N("dodgeStaminaUseModifier", s.m_dodgeStaminaUseModifier),
                N("swimStaminaUseModifier", s.m_swimStaminaUseModifier),
                N("sneakStaminaUseModifier", s.m_sneakStaminaUseModifier),
                N("healthRegenMultiplier", s.m_healthRegenMultiplier),
                N("staminaRegenMultiplier", s.m_staminaRegenMultiplier),
                N("eitrRegenMultiplier", s.m_eitrRegenMultiplier),
                N("healthUpFront", s.m_healthUpFront),
                N("healthOverTime", s.m_healthOverTime),
                N("healthOverTimeDuration", s.m_healthOverTimeDuration),
                N("staminaUpFront", s.m_staminaUpFront),
                N("staminaOverTime", s.m_staminaOverTime),
                N("staminaOverTimeDuration", s.m_staminaOverTimeDuration),
                N("eitrUpFront", s.m_eitrUpFront),
                N("eitrOverTime", s.m_eitrOverTime),
                N("eitrOverTimeDuration", s.m_eitrOverTimeDuration),
                N("addArmor", s.m_addArmor),
                "\"skillLevel\": " + Text.JsonString(s.m_skillLevel.ToString()),
                N("skillLevelModifier", s.m_skillLevelModifier),
                "\"skillLevel2\": " + Text.JsonString(s.m_skillLevel2.ToString()),
                N("skillLevelModifier2", s.m_skillLevelModifier2),
                "\"raiseSkill\": " + Text.JsonString(s.m_raiseSkill.ToString()),
                N("raiseSkillModifier", s.m_raiseSkillModifier),
                N("speedModifier", s.m_speedModifier),
                N("addMaxCarryWeight", s.m_addMaxCarryWeight),
                N("stealthModifier", s.m_stealthModifier),
                N("noiseModifier", s.m_noiseModifier),
                N("fallDamageModifier", s.m_fallDamageModifier),
                "\"percentDamage\": " + Damage(s.m_percentigeDamageModifiers),
                "\"mods\": " + Mods(s.m_mods),
            };
            return "{\n        " + string.Join(",\n        ", f) + "\n      }";
        }

        private static string N(string key, float value) => "\"" + key + "\": " + Text.Num(value);

        private static string Damage(HitData.DamageTypes d) =>
            "{ \"blunt\": " + Text.Num(d.m_blunt) + ", \"slash\": " + Text.Num(d.m_slash) +
            ", \"pierce\": " + Text.Num(d.m_pierce) + ", \"chop\": " + Text.Num(d.m_chop) +
            ", \"pickaxe\": " + Text.Num(d.m_pickaxe) + ", \"fire\": " + Text.Num(d.m_fire) +
            ", \"frost\": " + Text.Num(d.m_frost) + ", \"lightning\": " + Text.Num(d.m_lightning) +
            ", \"poison\": " + Text.Num(d.m_poison) + ", \"spirit\": " + Text.Num(d.m_spirit) + " }";

        private static string Mods(List<HitData.DamageModPair> mods)
        {
            if (mods == null || mods.Count == 0) return "[]";
            var parts = new List<string>();
            foreach (var m in mods)
                parts.Add("{ \"type\": " + Text.JsonString(m.m_type.ToString()) +
                          ", \"modifier\": " + Text.JsonString(m.m_modifier.ToString()) + " }");
            return "[" + string.Join(", ", parts) + "]";
        }
    }
}
