using System;
using System.IO;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using ValDataDumper.Dump;

using UnityEngine;

namespace ValDataDumper
{
    /// <summary>
    /// Read-only game-data dumper. Waits until a world is fully loaded (the ObjectDB is only
    /// complete in-world), dumps once, and can be re-run with a hotkey. No Harmony patches, no
    /// ServerSync, no publicizer — it only reads public game state, which is exactly why it
    /// survives API churn that breaks content-registration mods.
    /// </summary>
    [BepInPlugin(Guid, Name, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.tdmack.valdatadumper";
        public const string Name = "ValDataDumper";
        public const string PluginVersion = "0.9.0";

        internal static ManualLogSource Log;

        private ConfigEntry<bool> _dumpOnWorldLoad;
        private ConfigEntry<bool> _exportIcons;
        private ConfigEntry<string> _outputDir;
        private ConfigEntry<string> _versionOverride;
        private ConfigEntry<KeyCode> _hotkey;

        private bool _dumped;
        private bool _hotkeyUnavailable;

        private void Awake()
        {
            Log = Logger;

            _dumpOnWorldLoad = Config.Bind("General", "DumpOnWorldLoad", true,
                "Dump automatically the first time a world finishes loading.");
            _exportIcons = Config.Bind("General", "ExportIcons", true,
                "Export item/piece icons as PNGs (RenderTexture readback).");
            _outputDir = Config.Bind("General", "OutputDir", Path.Combine(Paths.ConfigPath, "valdatadumper"),
                "Output directory. Mirrors Jötunn's docs tree (data/objects, data/pieces, images/).");
            _versionOverride = Config.Bind("General", "VersionOverride", "",
                "Force the 'generated from Valheim X.Y.Z' stamp. Empty = read from the game.");
            _hotkey = Config.Bind("General", "Hotkey", KeyCode.F9,
                "Press in-world to re-run the dump.");

            Log.LogInfo($"{Name} {PluginVersion} loaded; output → {_outputDir.Value}");
        }

        private void Update()
        {
            // Auto-dump first: it must never be blocked by the hotkey path below.
            if (!_dumped && _dumpOnWorldLoad.Value
                && ZNetScene.instance != null && ObjectDB.instance != null && Player.m_localPlayer != null)
            {
                _dumped = true;
                RunDump("world-load");
                return;
            }

            if (_hotkeyUnavailable) return;
            try
            {
                if (Input.GetKeyDown(_hotkey.Value)) RunDump("hotkey");
            }
            catch (InvalidOperationException e)
            {
                // Legacy Input disabled (Unity Input System only) — auto-dump still works.
                _hotkeyUnavailable = true;
                Log.LogWarning($"hotkey disabled: {e.Message}");
            }
        }

        private void RunDump(string trigger)
        {
            try
            {
                string version = string.IsNullOrEmpty(_versionOverride.Value)
                    ? VersionStamp.Read()
                    : _versionOverride.Value;

                var stats = Dumper.Run(_outputDir.Value, version, _exportIcons.Value);
                Log.LogInfo(
                    $"[{trigger}] Valheim {version}: items={stats.Items} recipes={stats.Recipes} " +
                    $"pieceTables={stats.PieceTables} pieces={stats.Pieces} icons={stats.Icons} " +
                    $"iconFailures={stats.IconFailures} tokens={stats.Tokens} stats={stats.Stats} " +
                    $"itemExtras={stats.ItemExtras} pieceExtras={stats.PieceExtras} → {_outputDir.Value}");
            }
            catch (Exception e)
            {
                Log.LogError($"dump failed: {e}");
            }
        }
    }
}
