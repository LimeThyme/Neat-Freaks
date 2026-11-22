using System;
using System.Reflection;
using HarmonyLib;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace NeatFreaks
{
    public static class InventoryPatch
    {
        public const string Owner = "neatfreaks.inventory-icons";
        private static Harmony harmony;
        private static MethodInfo target;
        private static MethodInfo inventoryOpen;
        private static bool failed;

        // Patch image replacement and inventory opening only. An opening prefix
        // selects archives before DFU builds item panels, even with empty inventory.
        // Paperdoll rendering remains untouched; changed signatures fail clearly.
        public static void Install()
        {
            if (harmony != null) throw new InvalidOperationException("Already installed.");
            if (typeof(Harmony).Assembly.GetName().Version < new Version(2, 4, 2, 0))
                throw new InvalidOperationException("Daggerfall.Harmony with Harmony 2.4.2 or newer is required.");
            target = typeof(ItemHelper).GetMethod("GetInventoryImage", new[] { typeof(DaggerfallUnityItem) });
            if (target == null || target.ReturnType != typeof(ImageData)) throw new MissingMethodException("Unsupported GetInventoryImage signature.");
            inventoryOpen = typeof(DaggerfallInventoryWindow).GetMethod("OnPush", Type.EmptyTypes);
            if (inventoryOpen == null || inventoryOpen.ReturnType != typeof(void)) throw new MissingMethodException("Unsupported inventory OnPush signature.");
            harmony = new Harmony(Owner);
            failed = false;
            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(InventoryPatch).GetMethod("Prefix")));
                harmony.Patch(inventoryOpen, prefix: new HarmonyMethod(typeof(InventoryPatch).GetMethod("OnInventoryOpening")));
            }
            catch { Remove(); throw; }
        }

        // Do not cancel DFU's OnPush if optional discovery fails. The existing
        // image-request fallback and manual reload remain available for recovery.
        public static void OnInventoryOpening()
        {
            try { Icons.EnsureSelection("inventory opening"); }
            catch (Exception ex) { NeatFreaksLog.Error("Inventory-open archive scan failed: " + ex); }
        }

        public static bool Prefix(DaggerfallUnityItem item, ref ImageData __result)
        {
            if (failed) return true;
            try
            {
                ImageData replacement;
                if (!Icons.TryGet(item, out replacement)) return true;
                __result = replacement;
                return false;
            }
            catch (Exception ex)
            {
                failed = true;
                // Stop repeated exceptions from breaking every inventory refresh.
                // Let DFU render normally and report the first actionable failure.
                NeatFreaksLog.Error("Inventory replacement disabled after error: " + ex);
                return true;
            }
        }

        public static bool IsInstalled()
        {
            return HasOwnedPatch(target) && HasOwnedPatch(inventoryOpen);
        }
        private static bool HasOwnedPatch(MethodInfo method)
        {
            if (method == null) return false;
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return false;
            foreach (var patch in info.Prefixes) if (patch.owner == Owner) return true;
            return false;
        }

        // Remove only our owner ID so another mod's patches survive teardown.
        public static void Remove()
        {
            if (harmony != null && target != null) harmony.Unpatch(target, HarmonyPatchType.All, Owner);
            if (harmony != null && inventoryOpen != null) harmony.Unpatch(inventoryOpen, HarmonyPatchType.All, Owner);
            inventoryOpen = null;
            harmony = null;
            target = null;
        }
    }
}
