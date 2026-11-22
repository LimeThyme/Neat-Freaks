"""Audit external packs before installation. Requires Pillow and the game's ART_PAL.COL.

This is an independent artwork/manifest check, not a substitute for the C# decoder
tests. It deliberately reports all invalid authoring entries as failures, whereas
the runtime skips bad entries so unrelated inventory icons remain usable.
"""
import argparse
import json
import re
from pathlib import Path
import struct
import zlib
from PIL import Image


def chunks(path):
    raw = path.read_bytes()
    assert 0 < len(raw) <= 4 * 1024 * 1024, "PNG exceeds 4 MiB"
    assert raw[:8] == b"\x89PNG\r\n\x1a\n", "Bad PNG signature"
    pos, result = 8, []
    while pos < len(raw):
        assert len(raw) - pos >= 12, "Truncated chunk"
        size = struct.unpack_from(">I", raw, pos)[0]
        name, data = raw[pos + 4:pos + 8], raw[pos + 8:pos + 8 + size]
        assert pos + size + 12 <= len(raw), "Truncated payload"
        crc = struct.unpack_from(">I", raw, pos + 8 + size)[0]
        assert zlib.crc32(name + data) == crc, "CRC mismatch"
        result.append((name, data))
        pos += size + 12
    assert result[0][0] == b"IHDR" and result[-1] == (b"IEND", b""), "Chunk order/end"
    return result


def audit(root, palette_file, archive_filter=None):
    palette = palette_file.read_bytes()
    assert len(palette) in (768, 776), "Expected a 768/776-byte DFU palette"
    palette = palette[-768:]
    manifests = sorted(root.glob("*/pack.json"), key=lambda p: str(p.parent))
    assert 0 < len(manifests) <= 128, "Expected 1..128 packs"
    keys, results, errors = set(), [], []
    for manifest in manifests:
        try:
            assert manifest.stat().st_size <= 256 * 1024, "Manifest too large"
            pack = json.loads(manifest.read_text(encoding="utf-8-sig"))
            assert type(pack.get("schemaVersion")) is int and pack["schemaVersion"] == 3, "schemaVersion must be integer 3; migrate the manifest"
            archives = pack["archives"]
            assert isinstance(archives, list) and 1 <= len(archives) <= 128, "archives must have 1..128 entries"
            entries = []
            for row, archive in enumerate(archives, 1):
                try:
                    folder = archive["folder"]
                    assert "mode" not in archive, "Remove schema 2 mode"
                    enabled = archive["auto-indexing enabled"]
                    assert type(enabled) is bool or (isinstance(enabled, str) and enabled.lower() in ("true", "false")), "Expected true or false"
                    mode = "auto" if enabled is True or (isinstance(enabled, str) and enabled.lower() == "true") else "preserve"
                    target = archive["palette-processing type"]
                    assert isinstance(target, str) and target.lower() in ("clothing", "equipment"), "Invalid palette-processing type"
                    assert isinstance(folder, str), "Archive folder must be a string"
                    match = re.fullmatch(r'(0|[1-9][0-9]{0,2})(?:-\(([^()/\\:*?"<>|]+)\))?', folder)
                    assert match, "Use archive or archive-(description)"
                    label = match[2]
                    assert label is None or (label.strip() and label == label.strip() and not any(ord(c)<32 or 127<=ord(c)<=159 for c in label)), "Invalid description"
                    archive_number = int(match[1])
                    if archive_filter is not None and archive_number != archive_filter: continue
                    assert mode in ("indexed", "fixed", "auto", "preserve"), "Invalid archive mode"

                    directory = manifest.parent / folder
                    assert directory.is_dir(), "Missing archive directory"
                    for component in (directory, *directory.parents):
                        assert not component.is_symlink() and not (hasattr(component, "is_junction") and component.is_junction()), "Symlink/junction unsupported"
                    found = 0
                    for path in sorted(directory.iterdir(), key=lambda p: p.name):
                        if not path.is_file() or path.suffix.lower() != ".png": continue
                        match = re.fullmatch(r"(0|[1-9][0-9]{0,4})-0", path.stem)
                        if not match or int(match[1]) > 65535:
                            errors.append(f"{path}: Use canonical record-0.png; nonzero frames unsupported")
                            continue
                        entries.append(dict(archive=archive_number, record=int(match[1]), file=f"{folder}/{path.name}", mode=mode))
                        found += 1
                    assert found > 0, "Archive contains no supported PNGs"
                except Exception as exc:
                    errors.append(f"{manifest} archive #{row}: {exc}")
        except Exception as exc:
            errors.append(f"{manifest}: {exc}")
            continue
        for index, entry in enumerate(entries, 1):
            try:
                for field, maximum in (("archive", 999), ("record", 65535)):
                    assert type(entry[field]) is int and 0 <= entry[field] <= maximum, f"Invalid {field}"
                key = tuple(entry[f] for f in ("archive", "record"))
                assert key not in keys, f"Duplicate mapping {key} (runtime keeps first)"
                keys.add(key)
                assert len(keys) <= 512, "Global mapping limit exceeded"
                assert entry["mode"] in ("indexed", "fixed", "auto", "preserve"), "Unsupported mode"
                relative = entry["file"].replace("\\", "/")
                assert not relative.startswith("/") and ":" not in relative and all(p not in ("", ".", "..") for p in relative.split("/")), "Unsafe path"
                path = manifest.parent / relative
                assert path.suffix.lower() == ".png", "Expected .png"
                for component in (path, *path.parents):
                    assert not component.is_symlink() and not (hasattr(component, "is_junction") and component.is_junction()), "Symlink/junction unsupported"
                path.resolve().relative_to(manifest.parent.resolve())
                parts = chunks(path)
                width, height, depth, colour, compression, filtering, interlace = struct.unpack(">IIBBBBB", parts[0][1])
                assert 1 <= width <= 256 and 1 <= height <= 256, "Dimensions outside 1..256"
                assert depth == 8 and colour in (2, 3, 6) and compression == filtering == interlace == 0, "Unsupported PNG encoding"
                assert not any(name == b"acTL" for name, _ in parts), "Animated PNG unsupported"
                plte = next((data for name, data in parts if name == b"PLTE"), None)
                alpha = next((data for name, data in parts if name == b"tRNS"), None)
                if alpha is not None:
                    assert colour == 3, "Export truecolour transparency as RGBA"
                with Image.open(path) as image:
                    image.load()
                    rgba = image.convert("RGBA")
                    # Raw alpha bytes work across Pillow versions without deprecated getdata().
                    opaque = sum(alpha_byte != 0 for alpha_byte in rgba.tobytes()[3::4])
                    dye_pixels = 0
                    compatible = colour == 3 and plte == palette and alpha and alpha[0] == 0 and all(a == 255 for a in alpha[1:])
                    if entry["mode"] == "indexed" or (entry["mode"] == "preserve" and compatible):
                        assert colour == 3 and plte == palette, "Preserve all 256 ART_PAL.COL slots in order"
                        assert alpha and alpha[0] == 0 and all(a == 255 for a in alpha[1:]), "Index 0 must be the only transparent slot"
                        indices = image.tobytes()
                        if entry["mode"] == "indexed": assert 255 not in indices, "Legacy strict indexed mode rejects mask 255"
                        dye_pixels = sum(96 <= p <= 111 for p in indices)
                results.append(dict(pack=manifest.parent.name, mapping=list(key), file=relative, mode=entry["mode"], width=width, height=height, visible_pixels=opaque, dye_pixels=dye_pixels))
            except Exception as exc:
                errors.append(f"{manifest} entry #{index}: {exc}")
    return dict(ok=not errors, icons=results, errors=errors)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1] / "StreamingAssets/Textures/NeatFreaks")
    parser.add_argument("--palette", type=Path, required=True)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--archive", type=int, help="Audit only the archive selected for one player")
    args = parser.parse_args()
    report = audit(args.root, args.palette, args.archive)
    output = json.dumps(report, indent=2)
    print(output)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(output + "\n", encoding="utf-8")
    raise SystemExit(0 if report["ok"] else 1)
