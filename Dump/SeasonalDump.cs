using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace ValDataDumper.Dump
{
    /// <summary>
    /// `seasonal.json`: the game's seasonal content — Midsummer, Halloween, Yule and the like.
    ///
    /// Valheim keeps each event in a <see cref="SeasonalItemGroup"/> ScriptableObject: a start and
    /// end date (day, month; the event can wrap the new year), the build pieces it unlocks and the
    /// recipes it enables. Those pieces and recipes are otherwise switched off (`m_enabled` false);
    /// the player enables them only while a group's dates cover the current date. So a dump taken on
    /// an ordinary day shows them as disabled, and this file is the only place that says why.
    ///
    /// The groups hang off the Player prefab, which is always loaded, so
    /// `Resources.FindObjectsOfTypeAll` finds them without reading any private field. Dates come
    /// from the public `GetStartDate`/`GetEndDate` — only their day and month are written, since the
    /// year those return depends on when the dump runs.
    /// </summary>
    internal static class SeasonalDump
    {
        /// <summary>Write `seasonal.json`, keyed by group (asset) name. Returns the group count.</summary>
        public static int Write(string version, string path)
        {
            var rows = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var group in Resources.FindObjectsOfTypeAll<SeasonalItemGroup>())
            {
                if (group == null || rows.ContainsKey(group.name)) continue;
                var start = group.GetStartDate();
                var end = group.GetEndDate();

                var pieces = group.Pieces
                    .Where(p => p != null)
                    .Select(p => Text.JsonString(p.name))
                    .OrderBy(s => s, System.StringComparer.Ordinal);
                var recipes = group.Recipes
                    .Where(r => r != null)
                    .OrderBy(r => r.name, System.StringComparer.Ordinal)
                    .Select(r => "{ \"name\": " + Text.JsonString(r.name) +
                                 ", \"item\": " + Text.JsonString(r.m_item != null ? r.m_item.gameObject.name : "") + " }");

                var sb = new StringBuilder("{\n");
                sb.Append("      \"start\": { \"day\": ").Append(start.Day).Append(", \"month\": ").Append(start.Month).Append(" },\n");
                sb.Append("      \"end\": { \"day\": ").Append(end.Day).Append(", \"month\": ").Append(end.Month).Append(" },\n");
                sb.Append("      \"pieces\": [").Append(string.Join(", ", pieces)).Append("],\n");
                sb.Append("      \"recipes\": [").Append(string.Join(", ", recipes)).Append("]\n    }");
                rows[group.name] = sb.ToString();
            }
            ExtraDump.Write(path, version, "groups", rows);
            return rows.Count;
        }
    }
}
