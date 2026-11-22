using System;
using System.IO;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace NeatFreaks
{
    // Keep Unity objects here so the registry can be tested without graphics.
    public static class Icons
    {
        static IconLibrary<ImageData> library;
        static bool selectionPending = true;
        static ArchiveSelection.Profile activeProfile;
        static string characterLabel;
        public static string Root { get; private set; }
        public static int Count { get { return library == null ? 0 : library.Count; } }
        public static int CachedCount { get { return library == null ? 0 : library.CachedCount; } }

        public static void Load(DFPalette palette)
        {
            Clear();
            // Use one documented root so old test packs cannot silently override art.
            Root = Path.Combine(Application.streamingAssetsPath, "Textures", "NeatFreaks");
            library = new IconLibrary<ImageData>(palette, Render,
                image => { if (image.texture != null) UnityEngine.Object.Destroy(image.texture); },
                NeatFreaksLog.Warning, NeatFreaksLog.Detail, archive => activeProfile != null && activeProfile.Includes(archive), NeatFreaksLog.Info);
            // Startup can run before character creation/save restoration. Wait for
            // inventory opening, a real item request or HUD reload rather than a default race.
            selectionPending = true;
            NeatFreaksLog.Info("Archive selection deferred until first inventory opening, item request or HUD reload.");
        }
        // Inventory may contain no supported items, so window opening must be
        // able to trigger discovery independently. Once selected, later opens do
        // nothing: keep decoded images and GPU textures until explicit invalidation.
        public static void EnsureSelection(string trigger)
        {
            if (library == null || !selectionPending) return;
            NeatFreaksLog.Detail("Deferred archive scan triggered by " + trigger + ".");
            Reload(trigger == "inventory opening" ? "INVENTORY OPENED — INITIAL REGISTRATION" : "ITEM REQUEST — INITIAL REGISTRATION");
        }
        public static void Reload(string reason = "MANUAL RELOAD")
        {
            if (library == null) throw new InvalidOperationException("Icon library is not initialized.");
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            int warningsBefore = NeatFreaksLog.WarningCount;
            NeatFreaksLog.Info(reason + " — START");
            activeProfile = null;
            var player = GameManager.Instance.PlayerEntity;
            if (player == null || !ArchiveSelection.TryGetProfile(player.Gender, player.Race, out activeProfile))
            {
                library.Clear(); selectionPending = false; characterLabel = null;
                NeatFreaksLog.Warning("Player race/gender unavailable or unsupported; using vanilla icons until a save/new game or manual reload retries selection.");
                return;
            }
            string nextCharacter = player.Gender + " " + player.Race;
            NeatFreaksLog.Info("Character: " + (characterLabel != null && characterLabel != nextCharacter ? characterLabel + " -> " : "") + nextCharacter);
            characterLabel = nextCharacter;
            NeatFreaksLog.Detail("Player selection: race=" + player.Race + "; gender=" + player.Gender + "; " + activeProfile);
            try { library.LoadDirectory(Root); }
            finally { NeatFreaksLog.Flush(); }
            selectionPending = false;
            NeatFreaksLog.Info("Selected archives: Clothing " + activeProfile.Clothing + " (" + library.CountForArchive(activeProfile.Clothing) +
                "); Weapons " + activeProfile.Weapons + " (" + library.CountForArchive(activeProfile.Weapons) +
                "); Artifacts " + activeProfile.Artifacts + " (" + library.CountForArchive(activeProfile.Artifacts) +
                "); Armor " + activeProfile.Armor + " (" + library.CountForArchive(activeProfile.Armor) + ").");
            NeatFreaksLog.Info("Registered " + library.Count + " icons; skipped " + library.FilteredArchives + " archive folder declarations for other profiles.");
            NeatFreaksLog.Info("PNG loading: on demand. Conversion and palette processing follow each archive's schema 3 settings.");
            NeatFreaksLog.Info("REGISTRATION COMPLETE — " + elapsed.Elapsed.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                " ms; warnings=" + (NeatFreaksLog.WarningCount - warningsBefore) + ".");
            NeatFreaksLog.Flush();
        }
        // Save/new-game events invalidate old player textures. Rescan lazily after
        // DFU restores the character; manual HUD reload also re-reads race/gender.
        public static void InvalidatePlayerSelection()
        {
            if (library != null) library.Clear();
            activeProfile = null; characterLabel = null; selectionPending = true;
            NeatFreaksLog.Info("Previous character selection reset. Waiting for inventory opening or an item request.");
        }
        public static void Clear()
        {
            if (library != null) library.Clear();
            library = null; activeProfile = null; characterLabel = null; selectionPending = true;
        }
        public static bool TryGet(DaggerfallUnityItem item, out ImageData image)
        {
            image = default(ImageData);
            if (library == null || item == null || item.TemplateIndex > ItemHelper.LastDFTemplate) return false;
            int archive = item.InventoryTextureArchive;
            bool clothing = !item.IsArtifact && (item.ItemGroup == ItemGroups.MensClothing || item.ItemGroup == ItemGroups.WomensClothing);
            bool equipment = item.IsArtifact || item.ItemGroup == ItemGroups.Weapons || item.ItemGroup == ItemGroups.Armor;
            if (!clothing && !equipment) return false;
            EnsureSelection("item image request");
            // Use DFU's resolved record, including artifact MAGIC.DEF and clothing
            // variants. Its inventory-only katana correction differs from paperdoll.
            int record = InventoryRecord(item.InventoryTextureRecord, item.IsOfTemplate(ItemGroups.Weapons, (int)Weapons.Katana));
            try { return library.TryGet(archive, record, item.dyeColor, out image, item.IsArtifact); }
            finally { NeatFreaksLog.Flush(); }
        }
        public static int InventoryRecord(int record, bool katana)
        { return katana ? record + 1 : record; }

        public static DFBitmap ApplyDye(DFBitmap bitmap, DyeColors dye)
        {
            // Use DFU's palette swaps so fixed trim does not receive a whole-image tint.
            // Slots 96..111 change; fixed-colour slots and transparent index 0 survive.
            return ImageProcessing.ChangeDye(bitmap, dye, DyeTargets.Clothing);
        }
        // Pass the cached source DFBitmap directly into DFU before any
        // layout operation. ChangeMask removes index 255 first, like ItemHelper.
        // Both DFU routines return separate bitmaps; never redye the
        // previous rendered result. Kept graphics-independent for native tests.
        public static PngIcon PrepareForRender(PngIcon source, DyeColors dye, Action<string> diagnostic)
        {
            PngIcon processed = source;
            if (source.Indexed != null)
            {
                diagnostic("DFU mask start: ImageProcessing.ChangeMask; source=" + source.Width + "x" + source.Height + "; 255 -> 0");
                // Count reserved slots so the log distinguishes a successful call from
                // actual mask removal when diagnosing black rectangles.
                int removedMaskPixels = 0;
                foreach (byte index in source.Indexed.Data) if (index == 255) removedMaskPixels++;
                DFBitmap unmasked = ImageProcessing.ChangeMask(source.Indexed);
                diagnostic("DFU mask complete: " + unmasked.Width + "x" + unmasked.Height + "; removed-mask-pixels=" + removedMaskPixels);
                DFBitmap resultBitmap = unmasked;
                if (source.Processing == IconProcessing.Artifact)
                    diagnostic("DFU dye bypass: artifact retains authored palette after mask removal");
                else
                {
                    DyeTargets target = source.Processing == IconProcessing.WeaponsAndArmor ? DyeTargets.WeaponsAndArmor : DyeTargets.Clothing;
                    diagnostic("DFU dye start: source DFBitmap=" + unmasked.Width + "x" + unmasked.Height + "; dye=" + dye + "; target=" + target);
                    resultBitmap = ImageProcessing.ChangeDye(unmasked, dye, target);
                    diagnostic("DFU dye complete: ImageProcessing.ChangeDye returned " + resultBitmap.Width + "x" + resultBitmap.Height);
                }
                processed = new PngIcon { Width = resultBitmap.Width, Height = resultBitmap.Height, Indexed = resultBitmap };
            }
            else diagnostic("DFU mask/dye bypass: PNG colours unchanged; no compatible indexed source");
            PngIcon result = IconLayout.Prepare(processed);
            diagnostic("Inventory layout complete: " + source.Width + "x" + source.Height + " -> " + result.Width + "x" + result.Height);
            return result;
        }
        static ImageData Render(PngIcon icon, DyeColors dye)
        {
            icon = PrepareForRender(icon, dye, NeatFreaksLog.Detail);
            DFBitmap bitmap = icon.Indexed;
            Color32[] pixels = bitmap == null ? icon.FixedPixels : bitmap.GetColor32(0);
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(icon.Width, icon.Height, TextureFormat.ARGB32, false);
                texture.name = "NeatFreaks-" + (bitmap == null ? "fixed" : dye.ToString());
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                NeatFreaksLog.Detail("Texture uploaded: " + icon.Width + "x" + icon.Height + "; point filtering; ready for render cache");
                return new ImageData {
                    type = ImageTypes.TEXTURE, width = icon.Width * IconLayout.DisplayMultiplier, height = icon.Height * IconLayout.DisplayMultiplier,
                    hasAlpha = true, alphaIndex = 0, dfBitmap = bitmap, texture = texture,
                    size = new DaggerfallConnect.Utility.DFSize(icon.Width, icon.Height)
                };
            }
            catch
            {
                // A partially created GPU object must not leak when the entry falls back.
                if (texture != null) UnityEngine.Object.Destroy(texture);
                throw;
            }
        }
    }
}
