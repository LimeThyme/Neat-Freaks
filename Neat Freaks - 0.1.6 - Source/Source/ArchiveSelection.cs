using System;
using DaggerfallWorkshop.Game.Entity;
using System.IO;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;

namespace NeatFreaks
{
    public static class ArchiveSelection
    {
        // Keep the full folder name for disk access. Only the prefix identifies
        // the archive; the optional label is for authors, never race detection.
        public static int ParseFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) throw new InvalidDataException("Empty archive folder.");
            int split = folder.IndexOf("-(", StringComparison.Ordinal);
            string number = folder;
            if (split >= 0)
            {
                if (folder.Length <= split + 3 || !folder.EndsWith(")", StringComparison.Ordinal)) throw new InvalidDataException("Use archive-(description).");
                string label = folder.Substring(split + 2, folder.Length - split - 3);
                if (string.IsNullOrWhiteSpace(label) || label != label.Trim()) throw new InvalidDataException("Description must be nonempty without outer spaces.");
                foreach (char c in label)
                    if (char.IsControl(c) || "()/\\:*?\"<>|".IndexOf(c) >= 0) throw new InvalidDataException("Invalid archive description.");
                number = folder.Substring(0, split);
            }
            return IconLibrary<int>.ParseIndex(number, 999);
        }

        // A profile limits registration to the player's body and gender; image
        // loading still waits until an inventory item requests its exact record.
        public sealed class Profile
        {
            public readonly int Clothing, Weapons, Artifacts, Armor;
            public Profile(int clothing, int weapons, int artifacts, int armor)
            { Clothing=clothing; Weapons=weapons; Artifacts=artifacts; Armor=armor; }
            public bool Includes(int archive)
            { return archive==Clothing || archive==Weapons || archive==Artifacts || archive==Armor; }
            public override string ToString()
            { return "clothing="+Clothing+"; weapons="+Weapons+"; artifacts="+Artifacts+"; armor="+Armor; }
        }

        // Clothing and armor use DFU's body morphology; weapons/artifacts use
        // gender only. Keep artifact order explicit: it runs opposite to weapons.
        public static bool TryGetProfile(Genders gender, Races race, out Profile profile)
        {
            profile=null;
            int clothing;
            if (!TryGetArchive(gender, race, out clothing)) return false;
            bool female=gender==Genders.Female;
            int body=clothing-(female?235:239);
            profile=new Profile(clothing, female?233:234, female?433:432,
                (female?ItemBuilder.firstFemaleArchive:ItemBuilder.firstMaleArchive)+body);
            return true;
        }

        // Vanilla clothing templates start at 235 (women) or 239 (men).
        // Use DFU's own race-to-body mapping, so elves/humans share archives
        // exactly as ItemBuilder.SetRace does. Unknown races fail closed.
        public static bool TryGetArchive(Genders gender, Races race, out int archive)
        {
            archive = -1;
            if (gender != Genders.Female && gender != Genders.Male) return false;
            try { archive = (gender == Genders.Female ? 235 : 239) + (int)ItemBuilder.GetBodyMorphology(race); return true; }
            catch (Exception) { return false; }
        }
    }
}

