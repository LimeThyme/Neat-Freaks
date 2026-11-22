using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using FullSerializer;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Items;

namespace NeatFreaks
{
    public sealed class IconDefinition
    {
        public int Archive, Record;
        public string File, Mode;
        internal IconProcessing Processing;
        internal PngIcon Decoded;
        internal bool Failed;
        public string Key { get { return Archive + ":" + Record; } }
    }

    // Keep the registry independent of graphics so actual loading, failure and
    // cache lifecycle can be tested without substituting Unity or the DFU dye code.
    public sealed class IconLibrary<T>
    {
        public const int MaxIcons = 512;
        public const int MaxPacks = 128;
        public const int MaxManifestBytes = 256 * 1024;
        readonly Dictionary<string, IconDefinition> entries = new Dictionary<string, IconDefinition>();
        readonly Dictionary<string, T> cache = new Dictionary<string, T>();
        readonly Func<int, bool> includeArchive;
        readonly DFPalette palette;
        readonly Func<PngIcon, DyeColors, T> render;
        readonly Action<T> release;
        readonly Action<string> log;
        readonly Action<string> diagnostic;
        readonly Action<string> summary;
        public int FilteredArchives { get; private set; }
        public int Count { get { return entries.Count; } }
        // Counts registrations, not decoded images. Useful when a selected archive
        // has no supplied folder or reaches the unchanged global mapping limit.
        public int CountForArchive(int archive)
        {
            int count=0;
            foreach (IconDefinition entry in entries.Values) if (entry.Archive==archive) count++;
            return count;
        }
        public int CachedCount { get { return cache.Count; } }

        public IconLibrary(DFPalette palette, Func<PngIcon, DyeColors, T> render, Action<T> release, Action<string> log, Action<string> diagnostic = null, Func<int, bool> includeArchive = null, Action<string> summary = null)
        { this.summary = summary ?? (_ => { }); this.includeArchive = includeArchive ?? (_ => true); this.palette = palette; this.render = render; this.release = release; this.log = log; this.diagnostic = diagnostic ?? (_ => { }); }

        static fsData Field(fsData obj, string name)
        {
            fsData value;
            if (!obj.IsDictionary || !obj.AsDictionary.TryGetValue(name, out value)) throw new InvalidDataException("Missing field: " + name);
            return value;
        }
        static int Number(fsData obj, string name, int min, int max)
        {
            var value = Field(obj, name);
            if (!value.IsInt64 || value.AsInt64 < min || value.AsInt64 > max) throw new InvalidDataException("Invalid integer: " + name);
            return (int)value.AsInt64;
        }
        static string Text(fsData obj, string name)
        {
            var value = Field(obj, name);
            if (!value.IsString || string.IsNullOrWhiteSpace(value.AsString)) throw new InvalidDataException("Invalid string: " + name);
            return value.AsString;
        }

        // JSON booleans are preferred; quoted mixed-case values accommodate hand
        // editing without accepting unrelated values such as 1, yes or missing fields.
        static bool Boolean(fsData obj, string name)
        {
            var value = Field(obj, name);
            if (value.IsBool) return value.AsBool;
            if (value.IsString && string.Equals(value.AsString, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (value.IsString && string.Equals(value.AsString, "false", StringComparison.OrdinalIgnoreCase)) return false;
            throw new InvalidDataException(name + " must be true or false.");
        }

        // Log the author's settings separately from the resulting pixels. An off
        // switch can preserve valid indexes or leave RGB unchanged; those outcomes
        // must not look like old schema modes or imply native processing always ran.
        static string SettingsDescription(string mode, IconProcessing policy)
        {
            return "auto-indexing enabled=" + (mode == "auto" ? "true" : "false") +
                "; palette-processing type=" + (policy == IconProcessing.Clothing ? "clothing" : "equipment");
        }
        static string SourceDescription(IconDefinition entry)
        {
            if (entry.Decoded.Indexed == null) return "PNG colours unchanged; native palette processing bypassed";
            return entry.Mode == "auto" ? "auto-indexed DFU palette" : "existing DFU palette indexes preserved";
        }

        // Permit only pack-relative PNGs. Reject links too, including parent components:
        // lexical containment alone would permit a junction to escape the pack folder.
        public static void CheckNoLinks(string path) { CheckNoLinks(path, null); }

        // A scan shares ancestors across hundreds of PNGs. Remember only parents
        // that passed the complete walk; never cache a failed or missing path.
        // The leaf is always checked. Real reads use a fresh walk, so this small
        // scan-local shortcut cannot carry stale trust into later image requests.
        public static void CheckNoLinks(string path, HashSet<string> checkedParents)
        {
            string current = Path.GetFullPath(path);
            var passed = checkedParents == null ? null : new List<string>();
            bool leaf = true;
            while (!string.IsNullOrEmpty(current))
            {
                if (!leaf && checkedParents != null && checkedParents.Contains(current)) break;
                if (File.Exists(current) || Directory.Exists(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Symlinks/junctions are unsupported: " + current);
                    if (passed != null) passed.Add(current);
                }
                leaf = false;
                current = Path.GetDirectoryName(current);
            }
            if (passed != null) foreach (string value in passed) checkedParents.Add(value);
        }
        // Monotonic timings separate disk checks from image work; wall-clock log
        // gaps also include unrelated game activity and cannot prove a bottleneck.
        static string Milliseconds(Stopwatch watch)
        { return watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture); }

        static byte[] ReadBounded(string path, int limit)
        {
            CheckNoLinks(path);
            // Check and read through the same stream; a changing file cannot bypass the cap.
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length < 1 || stream.Length > limit) throw new InvalidDataException("File size outside allowed range: " + path);
                byte[] bytes = new byte[(int)stream.Length];
                int read = 0, n;
                while (read < bytes.Length && (n = stream.Read(bytes, read, bytes.Length - read)) > 0) read += n;
                if (read != bytes.Length || stream.ReadByte() != -1) throw new InvalidDataException("File changed while reading: " + path);
                return bytes;
            }
        }

        public void LoadDirectory(string root)
        {
            var scanTime = Stopwatch.StartNew();
            Clear();
            try
            {
                CheckNoLinks(root);
                if (!Directory.Exists(root)) { log("Pack directory missing: " + root + ". Install packs under StreamingAssets/Textures/NeatFreaks, then reload."); return; }
                // Sort pack folders so duplicate priority is reproducible. One-level
                // discovery avoids accidentally treating nested authoring files as packs.
                string[] folders = Directory.GetDirectories(root);
                Array.Sort(folders, StringComparer.Ordinal);
                int count = 0;
                foreach (string folder in folders)
                {
                    string manifest = Path.Combine(folder, "pack.json");
                    if (!File.Exists(manifest)) continue;
                    if (++count > MaxPacks) { log("Pack limit reached (128); remaining packs skipped."); break; }
                    try
                    {
                        byte[] raw = ReadBounded(manifest, MaxManifestBytes);
                        string json = new UTF8Encoding(false, true).GetString(raw).TrimStart('\ufeff');
                        LoadManifest(folder, json);
                    }
                    catch (Exception ex) { log("Pack skipped: " + manifest + ": " + ex.Message); }
                }
            }
            catch (Exception ex) { log("Pack scan failed: " + root + ": " + ex.Message); }
            finally { diagnostic("Timing: pack scan ms=" + Milliseconds(scanTime)); }
        }

        public void LoadManifest(string directory, string json)
        {
            // Read fields explicitly: missing or wrong JSON values must fail clearly,
            // rather than quietly using defaults that point at the wrong archive.
            fsData document;
            var parsed = fsJsonParser.Parse(json, out document);
            if (parsed.Failed) throw new InvalidDataException("Invalid JSON: " + parsed.FormattedMessages);
            // Reject old manifests explicitly; guessing their intent would hide migration mistakes.
            if (!Field(document, "schemaVersion").IsInt64 || Field(document, "schemaVersion").AsInt64 != 3)
                throw new InvalidDataException("schemaVersion must be 3. Migrate mode to auto-indexing enabled and palette-processing type; see the pack README.");
            int schema = 3;
            var archives = Field(document, "archives");
            if (!archives.IsList || archives.AsList.Count == 0 || archives.AsList.Count > 128)
                throw new InvalidDataException("archives must contain 1..128 folders.");
            diagnostic("Pack discovered: " + Path.Combine(directory, "pack.json"));
            var checkedParents = new HashSet<string>(StringComparer.Ordinal);
            int row = 0;
            foreach (fsData value in archives.AsList)
            {
                row++;
                var archiveTime = Stopwatch.StartNew();
                var validationTime = new Stopwatch();
                try
                {
                    string folder = Text(value, "folder");
                    int archive = ArchiveSelection.ParseFolder(folder);
                    // Filter before enumerating files or spending the mapping budget.
                    // Irrelevant archives can be large; their PNGs need no startup IO.
                    if (!includeArchive(archive)) { FilteredArchives++; diagnostic("Archive filtered: " + folder + "; archive=" + archive); continue; }
                    string mode;
                    IconProcessing policy;
                    {
                        if (value.AsDictionary.ContainsKey("mode")) throw new InvalidDataException("Schema 3 uses auto-indexing enabled and palette-processing type; remove mode.");
                        mode = Boolean(value, "auto-indexing enabled") ? "auto" : "preserve";
                        string target = Text(value, "palette-processing type");
                        if (string.Equals(target, "clothing", StringComparison.OrdinalIgnoreCase)) policy = IconProcessing.Clothing;
                        else if (string.Equals(target, "equipment", StringComparison.OrdinalIgnoreCase)) policy = IconProcessing.WeaponsAndArmor;
                        else throw new InvalidDataException("palette-processing type must be clothing or equipment.");
                    }
                    string path = Path.Combine(directory, folder);
                    validationTime.Start();
                    CheckNoLinks(path, checkedParents);
                    validationTime.Stop();
                    if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Archive folder missing: " + path);
                    // Explicit folders only; direct PNG children only. Sort files so duplicate
                    // aliases/extension case have deterministic first-wins behaviour too.
                    string[] files = Directory.GetFiles(path);
                    Array.Sort(files, StringComparer.Ordinal);
                    int added = 0;
                    foreach (string file in files)
                    {
                        if (!string.Equals(Path.GetExtension(file), ".png", StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            validationTime.Start();
                            try { CheckNoLinks(file, checkedParents); }
                            finally { validationTime.Stop(); }
                            string[] parts = Path.GetFileNameWithoutExtension(file).Split('-');
                            if (parts.Length != 2) throw new InvalidDataException("Use record-0.png filenames.");
                            int record = ParseIndex(parts[0], 65535), frame = ParseIndex(parts[1], 65535);
                            if (frame != 0) throw new InvalidDataException("Only static frame 0 is supported; nonzero frames are not alternate styles.");
                            var entry = new IconDefinition { Archive = archive, Record = record, File = file, Mode = mode, Processing = policy };
                            if (entries.ContainsKey(entry.Key)) throw new InvalidDataException("Duplicate mapping " + entry.Key + "; first definition retained.");
                            if (entries.Count >= MaxIcons) { log("Global icon limit reached (512); remaining PNGs in " + path + " skipped."); break; }
                            entries.Add(entry.Key, entry);
                            added++;
                        }
                        catch (Exception ex) { log("PNG skipped: " + file + ": " + ex.Message); }
                    }
                    diagnostic("Archive discovered: " + path + "; schema=" + schema + "; " + SettingsDescription(mode, policy) + "; added=" + added);
                    diagnostic("Timing: archive=" + archive + "; total-ms=" + Milliseconds(archiveTime) +
                        "; validation-ms=" + Milliseconds(validationTime) + "; remembered-paths=" + checkedParents.Count);
                    if (added == 0) log("Archive registered no new icons: " + path);
                }
                catch (Exception ex) { log("Archive skipped: " + directory + "/pack.json #" + row + ": " + ex.Message); }
            }
        }

        public static int ParseIndex(string text, int maximum)
        {
            // Strict ASCII decimal rejects traversal, signs, spaces and aliases with
            // leading zeros. Canonical names remain portable across filesystems.
            if (string.IsNullOrEmpty(text) || text.Length > 5 || (text.Length > 1 && text[0] == '0'))
                throw new InvalidDataException("Use a canonical decimal index: " + text);
            foreach (char c in text) if (c < '0' || c > '9') throw new InvalidDataException("Invalid numeric index: " + text);
            int number;
            if (!int.TryParse(text, out number) || number > maximum) throw new InvalidDataException("Index outside supported range: " + text);
            return number;
        }

        public bool TryGet(int archive, int record, DyeColors dye, out T image, bool artifact = false)
        {
            image = default(T);
            IconDefinition entry;
            if (!entries.TryGetValue(archive + ":" + record, out entry) || entry.Failed) return false;
            // Invalid dye values must not grow the cache or poison a valid icon entry.
            if (!Enum.IsDefined(typeof(DyeColors), dye)) return false;
            // Artifacts preserve their authored palette after mask removal, like DFU.
            // Include policy in the key so an artifact cannot reuse a metal-tinted render.
            IconProcessing processing = entry.Processing;
            bool isArtifact = artifact && processing == IconProcessing.WeaponsAndArmor;
            if (isArtifact) processing = IconProcessing.Artifact;
            string key = entry.Key + ":" + ((entry.Decoded != null && entry.Decoded.Indexed == null) || isArtifact ? 0 : (int)dye) + (isArtifact ? ":artifact" : "");
            if (cache.TryGetValue(key, out image)) return true;
            try
            {
                if (entry.Decoded == null)
                {
                    var stageTime = Stopwatch.StartNew();
                    byte[] bytes = ReadBounded(entry.File, PngIcon.MaxBytes);
                    diagnostic("Timing: " + entry.Key + "; read-and-validation-ms=" + Milliseconds(stageTime));
                    stageTime.Restart();
                    entry.Decoded = PngIcon.Decode(bytes, false, palette, entry.Mode == "preserve");
                    diagnostic("Timing: " + entry.Key + "; decode-ms=" + Milliseconds(stageTime));
                    // Convert once so additional dye colours reuse the same indexed source.
                    if (entry.Mode == "auto")
                    {
                        diagnostic("Auto-index start: " + entry.Key + "; source=" + entry.File);
                        stageTime.Restart();
                        entry.Decoded = AutoIndexer.Convert(entry.Decoded, palette, message => diagnostic(entry.Key + ": " + message));
                        diagnostic("Timing: " + entry.Key + "; auto-index-ms=" + Milliseconds(stageTime));
                    }
                    diagnostic("PNG decoded: " + entry.File + "; " + entry.Decoded.Width + "x" + entry.Decoded.Height + "; " + SettingsDescription(entry.Mode, entry.Processing) + "; source=" + SourceDescription(entry));
                }
                // A no-conversion RGB image has no dye slots. Collapse its cache key
                // after decoding so later dye requests share one unchanged render.
                if (entry.Decoded.Indexed == null) key = entry.Key + ":0" + (isArtifact ? ":artifact" : "");
                if (cache.TryGetValue(key, out image)) return true;
                diagnostic("Processing selected: " + entry.Key + "; target=" + processing +
                    "; source=" + SourceDescription(entry));
                diagnostic("Render start: " + key + "; " + SettingsDescription(entry.Mode, entry.Processing) + "; source=" + SourceDescription(entry));
                var renderTime = Stopwatch.StartNew();
                // Wrap shared source data rather than changing its policy or pixels.
                // Each material starts from the same base, never a previously dyed image.
                image = render(new PngIcon { Width=entry.Decoded.Width, Height=entry.Decoded.Height,
                    Indexed=entry.Decoded.Indexed, FixedPixels=entry.Decoded.FixedPixels, Processing=processing }, dye);
                diagnostic("Timing: " + key + "; render-ms=" + Milliseconds(renderTime));
                cache.Add(key, image);
                diagnostic("Render cached: " + key + "; " + SettingsDescription(entry.Mode, entry.Processing));
                return true;
            }
            catch (Exception ex)
            {
                // A missing/invalid file or render failure disables only this mapping until
                // explicit reload. Other styles and packs continue to work, without log spam.
                entry.Failed = true;
                log("Could not load icon " + entry.Key + ": " + ex.Message + ". Using vanilla icon until reload.");
                diagnostic("Icon failure: source=" + entry.File + "; exception=" + ex);
                return false;
            }
        }
        public void Clear()
        {
            // Name exactly what is discarded: character selection belongs to Icons.
            summary(entries.Count == 0 && cache.Count == 0 ? "Icon cache already empty." :
                "Cleared previous icons: " + entries.Count + " mappings, " + cache.Count + " rendered variants; character selection is separate.");
            FilteredArchives = 0;
            diagnostic("Cache clear: registered mappings=" + entries.Count + "; rendered entries=" + cache.Count);
            foreach (T image in cache.Values) release(image);
            cache.Clear(); entries.Clear();
        }
    }
}
