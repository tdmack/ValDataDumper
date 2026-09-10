using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ValDataDumper.Dump
{
    /// <summary>Small text helpers shared by the writers.</summary>
    internal static class Text
    {
        /// <summary>The literal Jötunn uses for an absent value.</summary>
        public const string Null = "NULL";

        /// <summary>
        /// Make a string safe as a single markdown-table cell: no pipes, no line breaks.
        /// Rich-text tags (<c>&lt;color=…&gt;</c>) are kept — Jötunn keeps them too.
        /// </summary>
        public static string Cell(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('|', '/').Trim();
        }

        /// <summary>A cell that is <see cref="Null"/> when empty.</summary>
        public static string CellOrNull(string s)
        {
            var c = Cell(s);
            return c.Length == 0 ? Null : c;
        }

        /// <summary>
        /// Deterministic 32-hex stand-in for Jötunn's Unity AssetID (unreadable at runtime).
        /// Nothing is known to consume the value, but every parser expects the column.
        /// </summary>
        public static string PseudoAssetId(string prefab)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(prefab ?? ""));
                var sb = new StringBuilder(32);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Jötunn's resource list: <c>&lt;ul&gt;&lt;li&gt;3 Wood&lt;/li&gt;…&lt;/ul&gt;</c>.</summary>
        public static string ResourceList(IEnumerable<KeyValuePair<string, int>> items)
        {
            var sb = new StringBuilder("<ul>");
            foreach (var kv in items) sb.Append("<li>").Append(kv.Value).Append(' ').Append(Cell(kv.Key)).Append("</li>");
            return sb.Append("</ul>").ToString();
        }

        /// <summary>
        /// A float as JSON: invariant culture (a comma decimal separator on a localized
        /// machine would corrupt the file) and no exponent notation.
        /// </summary>
        public static string Num(float value)
        {
            if (value == (int)value) return ((int)value).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        /// <summary>Minimal JSON string escaping (no external JSON dependency in a BepInEx plugin).</summary>
        public static string JsonString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var ch in s ?? "")
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
