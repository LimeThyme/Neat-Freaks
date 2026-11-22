"""Check build inputs, release metadata, developer documentation and SHA256 inventory.

Standard library only. Run from any directory. Optional --baseline verifies the
unchanged PNG decoder and corrected sprite bytes (standard library only).
"""
import argparse
import hashlib
import json
from pathlib import Path

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def strip_comments(text):
    import re
    return re.sub(r"\s+", "", re.sub(r"//[^\n]*", "", text.replace("public enum IconProcessing { Clothing, WeaponsAndArmor, Artifact }", "").replace("public IconProcessing Processing;", "")))

def validate(root, baseline=None):
    unity = root / "Unity"
    mod = unity / "Assets/Untracked/ModBuilder/Neat Freaks - 0.1.6"
    manifest = json.loads((mod / "neatfreaks.dfmod.json").read_text(encoding="utf-8-sig"))
    assert manifest["ModVersion"] == "0.1.6" and manifest["DFUnity_Version"] == "1.1.1"
    assert manifest["GUID"] == "39513e40-fde7-47c4-9ec1-d88c9c3f4380"
    files = manifest["Files"]
    assert len(files) == len(set(files)) == 11, "Expected nine C# files, settings and mod manifest"
    actual = {p.relative_to(unity).as_posix() for p in mod.rglob("*") if p.is_file()}
    assert set(files) == actual, "Build input inventory mismatch"
    assert all((unity / name).is_file() for name in files), "Missing build asset"
    dependency = manifest["Dependencies"]
    assert dependency == [dict(Name="daggerfall.harmony", IsOptional=False, IsPeer=False)], "Dependency mismatch"
    for name in ("README.md", "BUILD.md", "CHANGELOG.md", "SOURCE-CHANGES.md", "PNG-PACKS.md", "TROUBLESHOOTING.md", "VALIDATION.md", "DEBUG-LOGGING.md", "PERFORMANCE.md", "DeveloperTests/README.md"):
        assert (root / name).is_file(), f"Missing documentation: {name}"
    assert not any(p.suffix.lower() in (".dll", ".exe", ".dfmod", ".bytes") for p in root.rglob("*") if p.is_file()), "Compiled/dependency/legacy payload in source kit"
    source = "\n".join(p.read_text(encoding="utf-8-sig") for p in (mod / "Source").glob("*.cs"))
    assert "JsonUtility" not in source.replace("No Unity JsonUtility", ""), "Legacy serializer reference"
    assert "ImageProcessing.ChangeDye" in source and 'Path.Combine(Application.streamingAssetsPath, "Textures", "NeatFreaks")' in source
    assert 'Path.Combine(mod.DirPath, "NeatFreaks")' not in source, "Legacy pack root restored"
    assert 'neatfreaks_reload' not in source, "Removed command remains"
    assert 'RegisterCommand("neatfreaks", RootCommand' in source, "Missing command dispatch"
    for event, handler in (("SaveLoadManager.OnLoad", "OnSaveLoaded"), ("StartGameBehaviour.OnStartGame", "OnGameStarted")):
        assert event + " += " + handler in source and event + " -= " + handler in source, "Unbalanced lifecycle subscriptions"
    assert 'typeof(DaggerfallInventoryWindow).GetMethod("OnPush", Type.EmptyTypes)' in source, "Missing inventory-open target"
    assert 'Icons.EnsureSelection("inventory opening")' in source, "Missing opening trigger"
    assert 'EnsureSelection("item image request")' in source, "Lost item fallback"
    assert "writer.AutoFlush = false" in source and "bufferedLines >= 32" in source
    assert "Timing: pack scan ms=" in source and "CheckNoLinks(file, checkedParents)" in source
    assert "IsEquipmentArchive" not in source
    assert "InventoryRecord(item.InventoryTextureRecord" in source
    assert "DyeTargets.WeaponsAndArmor" in source and "IconProcessing.Artifact" in source
    assert "effective-mode=fixed" not in source
    settings = json.loads((mod / "modsettings.json").read_text(encoding="utf-8"))
    key = settings["Sections"][0]["Keys"][0]
    assert key["Name"] == "Enable Mod Logging" and key["Value"] is False and key["$type"].endswith(".ToggleKey")
    verbose = settings["Sections"][0]["Keys"][1]
    assert len(settings["Sections"][0]["Keys"]) == 2
    assert verbose["Name"] == "Verbose Logging" and verbose["Value"] is False
    assert verbose["$type"].endswith(".ToggleKey")
    assert 'NeatFreaksLog.SetVerbose(settings.GetValue<bool>("Options", "Verbose Logging"))' in source
    assert 'NeatFreaksLog.Detail' in source and 'REGISTRATION COMPLETE' in source
    pack = root / "StreamingAssets/Textures/NeatFreaks/Vanilla/pack.json"
    assert pack.with_name("README.md").is_file()
    for manifest_path in [pack, *list((root / "Examples").glob("*.json"))]:
        data = json.loads(manifest_path.read_text(encoding="utf-8"))
        assert data["schemaVersion"] == 3
        for row in data["archives"]:
            assert "mode" not in row and type(row["auto-indexing enabled"]) is bool
            assert row["palette-processing type"] in ("clothing", "equipment")
    assert "; mode=" not in source, "Obsolete runtime diagnostic label"
    assert "auto-indexing enabled=" in source and "palette-processing type=" in source
    assert "existing DFU palette indexes preserved" in source
    expected = {}
    for line in (root / "SHA256SUMS.txt").read_text().splitlines():
        sha, name = line.split("  ", 1)
        assert name not in expected and not Path(name).is_absolute() and ".." not in Path(name).parts, "Invalid checksum path"
        expected[name] = sha
    actual_files = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt"}
    assert actual_files == set(expected), "Checksum inventory differs"
    for name, sha in expected.items():
        assert digest(root / name) == sha, f"Hash mismatch: {name}"
    print(f"PASS: package inventory; 11 build inputs; {len(expected)} checksummed files; no bundled binaries")
    if baseline:
        old = baseline / "Unity/Assets/Untracked/ModBuilder/Neat Freaks - 0.0.2"
        for record in range(16, 21):
            assert (baseline / f"External/NeatFreaks/Vanilla/235-{record}.png").read_bytes() == (root / f"StreamingAssets/Textures/NeatFreaks/Vanilla/235/{record}-0.png").read_bytes(), "Corrected artwork differs"
        print("PASS: All five corrected PNG files byte-identical to 0.0.2; new folder layout verified")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--baseline", type=Path)
    args = parser.parse_args()
    validate(args.root, args.baseline)
