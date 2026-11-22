"""Losslessly migrate an old NFIX icon to an 8-bit indexed PNG. Requires Pillow.

No nearest-colour quantization: palette indices, dimensions and transparent index
0 are retained exactly. This is a development utility; runtime only reads PNG.
"""
import argparse
from pathlib import Path
import struct
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("destination", type=Path)
parser.add_argument("--palette", type=Path, required=True)
parser.add_argument("--dyeable-light-brown", action="store_true", help="Map the 64..79 brown ramp to clothing dye slots 96..111; reject other visible indices")
args = parser.parse_args()
raw = args.source.read_bytes()
assert len(raw) >= 8 and raw[:4] == b"NFIX", "Invalid NFIX header"
w, h = struct.unpack_from("<HH", raw, 4)
assert 1 <= w <= 256 and 1 <= h <= 256 and len(raw) == 8 + w * h, "Invalid size"
assert 255 not in raw[8:], "Mask index 255 unsupported"
palette = args.palette.read_bytes()
assert len(palette) in (768, 776), "Expected ART_PAL.COL"
pixels = raw[8:]
if args.dyeable_light_brown:
    # Explicit authoring correction, not a global runtime recolouring rule.
    # LightBrown maps 96..111 back to 64..79 with the same shade offsets in DFU.
    assert all(p == 0 or 64 <= p <= 79 for p in pixels), "Expected only transparent and light-brown pixels"
    pixels = bytes(p + 32 if p else 0 for p in pixels)
image = Image.frombytes("P", (w, h), pixels)
image.putpalette(palette[-768:])
args.destination.parent.mkdir(parents=True, exist_ok=True)
image.save(args.destination, transparency=0, bits=8)
with Image.open(args.destination) as check:
    assert check.mode == "P" and check.size == (w, h) and check.tobytes() == pixels, "Roundtrip mismatch"
print(f"PASS: {args.destination}; {w}x{h}; verified indices; transparent slot 0; brown-to-dye={args.dyeable_light_brown}")
