using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace NeatFreaks
{
    // Keep Harmony types in InventoryPatch so a missing provider can be reported
    // before patch setup instead of preventing bootstrap from loading.
    public sealed class Bootstrap : MonoBehaviour
    {
        private static Bootstrap instance;
        private Mod owner;
        private bool installed;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams args)
        {
            if (instance != null) return;
            var go = new GameObject("Neat Freaks 0.1.6");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Bootstrap>();
            instance.owner = args.Mod;
            args.Mod.IsReady = false;
            // Read the saved opt-in first so startup respects the logging preference. Settings
            // changes affect logging only; they do not reset the icon cache or hook.
            NeatFreaksLog.BeginSession(args.Mod.DirPath);
            args.Mod.LoadSettingsCallback = instance.ApplySettings;
            try { args.Mod.LoadSettings(); }
            catch (Exception ex) { NeatFreaksLog.Warning("Cannot load logging preference; logging remains off. " + ex.Message); }
            NeatFreaksLog.Info("Starting Neat Freaks 0.1.6. Unity: " + Application.unityVersion + " (Unity runtime)");
        }

        private void OnSaveLoaded(SaveData_v1 data) { NeatFreaksLog.Info("SAVE LOADED"); Icons.InvalidatePlayerSelection(); NeatFreaksLog.Flush(); }
        private void OnGameStarted(object sender, EventArgs args) { NeatFreaksLog.Info("NEW GAME STARTED"); Icons.InvalidatePlayerSelection(); NeatFreaksLog.Flush(); }

        private void ApplySettings(ModSettings settings, ModSettingsChange change)
        {
            // Set detail preference first so enabling logging respects it immediately.
            // Changing either checkbox leaves the icon selection and cache alone.
            NeatFreaksLog.SetVerbose(settings.GetValue<bool>("Options", "Verbose Logging"));
            NeatFreaksLog.SetEnabled(settings.GetValue<bool>("Options", "Enable Mod Logging"));
            NeatFreaksLog.Info("Logging mode: " + (settings.GetValue<bool>("Options", "Verbose Logging") ? "verbose" : "normal"));
            NeatFreaksLog.Flush();
        }

        private IEnumerator Start()
        {
            Mod provider = null;
            foreach (var mod in ModManager.Instance.GetAllMods())
                if (mod.FileName == "daggerfall.harmony") provider = mod;
            if (provider == null || !provider.Enabled || provider.LoadPriority >= owner.LoadPriority)
            {
                Fail("Enable Daggerfall.Harmony and place it before Neat Freaks.");
                yield break;
            }
            var providerType = provider.GetCompiledType("Daggerfall.Harmony.SubModule");
            if (providerType == null) { Fail("Unsupported Harmony provider: missing SubModule."); yield break; }
            MonoBehaviour activeProvider = null;
            foreach (var obj in UnityEngine.Object.FindObjectsOfType(providerType))
            {
                var component = obj as MonoBehaviour;
                if (component != null && component.isActiveAndEnabled) activeProvider = component;
            }
            if (activeProvider == null) { Fail("Harmony's startup component is not active."); yield break; }
            // Supported provider clears old patches in Start. Wait for that Start to finish.
            yield return null;
            if (activeProvider == null || !activeProvider.isActiveAndEnabled || !provider.Enabled)
            { Fail("Harmony became unavailable during startup."); yield break; }
            try
            {
                var palette = new DFPalette();
                if (!palette.Load(Path.Combine(DaggerfallUnity.Instance.Arena2Path, "ART_PAL.COL")))
                    throw new InvalidDataException("Cannot load the game's ART_PAL.COL.");
                Icons.Load(palette);
                InventoryPatch.Install();
                installed = true;
                SaveLoadManager.OnLoad += OnSaveLoaded;
                StartGameBehaviour.OnStartGame += OnGameStarted;
            }
            catch (Exception ex) { Fail(ex.ToString()); yield break; }
            yield return null;
            try
            {
                if (!InventoryPatch.IsInstalled()) throw new InvalidOperationException("Inventory hook was removed during startup.");
                owner.IsReady = true;
                RegisterReload();
                NeatFreaksLog.Notice("0.1.6 ready: " + Icons.Count + " external icon mappings; schema 3 pack-controlled processing. Root: " + Icons.Root);
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }

        // The console database has no unregister API. Keep a static callback
        // which resolves the current instance and safely rejects calls after teardown.
        private static void RegisterReload()
        {
            if (!Wenzil.Console.ConsoleCommandsDatabase.HasCommand("neatfreaks"))
                Wenzil.Console.ConsoleCommandsDatabase.RegisterCommand("neatfreaks", RootCommand,
                    "Neat Freaks commands.", "neatfreaks reload");
        }

        // The console registers the first word; dispatch the subcommand here so
        // the public spelling is consistently "neatfreaks reload".
        private static string RootCommand(params string[] args)
        {
            if (args.Length == 1 && args[0] == "reload") return ReloadCommand();
            return "Usage: neatfreaks reload";
        }

        private static string ReloadCommand(params string[] args)
        {
            if (args.Length != 0) return "Usage: neatfreaks reload";
            if (instance == null || !instance.installed || !instance.owner.IsReady) return "Neat Freaks is not ready.";
            // Open item panels still hold texture references. Require the HUD before
            // releasing textures, then let newly opened windows request fresh images.
            if (!GameManager.Instance.IsPlayerOnHUD) return "Close inventory, trade and other windows before reloading.";
            try
            {
                Icons.Reload("MANUAL RELOAD");
                return "Neat Freaks reloaded: " + Icons.Count + " mappings. Reopen inventory to refresh icons.";
            }
            catch (Exception ex) { NeatFreaksLog.Error("Reload failed: " + ex); return "Neat Freaks reload failed: " + ex.Message; }
        }

        private void Fail(string message)
        {
            NeatFreaksLog.Error(message + " Original inventory rendering remains available.");
            if (owner != null) owner.IsReady = false;
            SaveLoadManager.OnLoad -= OnSaveLoaded;
            StartGameBehaviour.OnStartGame -= OnGameStarted;
            if (installed) { InventoryPatch.Remove(); installed = false; }
            Icons.Clear();
        }

        private void OnApplicationQuit() { NeatFreaksLog.EndSession(); }

        private void OnDestroy()
        {
            if (instance != this) return;
            SaveLoadManager.OnLoad -= OnSaveLoaded;
            StartGameBehaviour.OnStartGame -= OnGameStarted;
            if (installed) InventoryPatch.Remove();
            Icons.Clear();
            if (owner != null) owner.IsReady = false;
            if (owner != null) owner.LoadSettingsCallback = null;
            NeatFreaksLog.EndSession();
            instance = null;
        }
    }

}

