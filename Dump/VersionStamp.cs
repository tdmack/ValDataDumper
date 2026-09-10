using System;
using System.Reflection;
using System.Text.RegularExpressions;

using UnityEngine;

namespace ValDataDumper.Dump
{
    /// <summary>
    /// Resolves the game's version string ("1.0.7") via reflection so a signature change in
    /// <c>Version.GetVersionString</c> degrades to a fallback instead of a compile break.
    /// </summary>
    internal static class VersionStamp
    {
        private static readonly Regex Semver = new Regex(@"\d+\.\d+\.\d+");

        public static string Read()
        {
            string raw = TryGetVersionString() ?? Application.version ?? "";
            var m = Semver.Match(raw);
            if (!m.Success)
            {
                throw new InvalidOperationException(
                    $"could not find an X.Y.Z version in '{raw}' — set VersionOverride in the config");
            }
            return m.Value;
        }

        private static string TryGetVersionString()
        {
            try
            {
                var type = typeof(ZNet).Assembly.GetType("Version");
                if (type == null) return null;
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != "GetVersionString" || method.ReturnType != typeof(string)) continue;
                    var ps = method.GetParameters();
                    var args = new object[ps.Length];
                    for (int i = 0; i < ps.Length; i++)
                    {
                        args[i] = ps[i].HasDefaultValue
                            ? ps[i].DefaultValue
                            : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
                    }
                    return method.Invoke(null, args) as string;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Version.GetVersionString lookup failed: {e.Message}");
            }
            return null;
        }
    }
}
