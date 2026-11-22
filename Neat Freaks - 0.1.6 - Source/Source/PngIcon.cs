using System;
using System.IO;
using System.IO.Compression;
using DaggerfallConnect;
using UnityEngine;

namespace NeatFreaks
{
    // Preserve palette slot identity because Unity LoadImage only retains colours,
    // which cannot distinguish equal RGB colours assigned to different dye slots.
    public enum IconProcessing { Clothing, WeaponsAndArmor, Artifact }

    public sealed class PngIcon
    {
        public int Width, Height;
        // A render view carries policy; decoded pixels remain shared and immutable.
        public IconProcessing Processing;
        public DFBitmap Indexed;
        public Color32[] FixedPixels; // Keep Unity row order so upload needs no extra flip.
        public const int MaxBytes = 4 * 1024 * 1024;
        public const int MaxDimension = 256;

        static int U32(byte[] b, int p)
        {
            uint n = ((uint)b[p] << 24) | ((uint)b[p + 1] << 16) | ((uint)b[p + 2] << 8) | b[p + 3];
            if (n > int.MaxValue) throw new InvalidDataException("PNG integer overflow.");
            return (int)n;
        }
        static uint CRC(byte[] b, int p, int length)
        {
            uint crc = 0xffffffff;
            for (int i = p; i < p + length; i++)
            {
                crc ^= b[i];
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            }
            return ~crc;
        }
        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        public static PngIcon Decode(byte[] bytes, bool indexed, DFPalette palette, bool preserveIndexes = false)
        {
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (bytes == null || bytes.Length < 33 || bytes.Length > MaxBytes) throw new InvalidDataException("Invalid PNG file size.");
            for (int i = 0; i < 8; i++) if (bytes[i] != signature[i]) throw new InvalidDataException("Not a PNG.");
            var result = new PngIcon();
            byte[] colours = null, alpha = null;
            int type = -1, pos = 8;
            bool header = false, end = false, data = false, dataEnded = false;
            using (var compressed = new MemoryStream())
            {
                while (pos < bytes.Length)
                {
                    if (bytes.Length - pos < 12) throw new InvalidDataException("Truncated PNG chunk.");
                    int size = U32(bytes, pos);
                    if (size > bytes.Length - pos - 12) throw new InvalidDataException("Truncated PNG payload.");
                    string name = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
                    int start = pos + 8, crcPos = start + size;
                    uint stored = ((uint)bytes[crcPos] << 24) | ((uint)bytes[crcPos + 1] << 16) | ((uint)bytes[crcPos + 2] << 8) | bytes[crcPos + 3];
                    if (CRC(bytes, pos + 4, size + 4) != stored) throw new InvalidDataException("PNG CRC mismatch: " + name);
                    if (!header && name != "IHDR") throw new InvalidDataException("IHDR must be first.");
                    if (data && name != "IDAT") dataEnded = true;
                    switch (name)
                    {
                        case "IHDR":
                            if (header || size != 13) throw new InvalidDataException("Invalid IHDR.");
                            result.Width = U32(bytes, start); result.Height = U32(bytes, start + 4);
                            type = bytes[start + 9];
                            if (result.Width < 1 || result.Height < 1 || result.Width > MaxDimension || result.Height > MaxDimension ||
                                bytes[start + 8] != 8 || (type != 3 && type != 2 && type != 6) ||
                                bytes[start + 10] != 0 || bytes[start + 11] != 0 || bytes[start + 12] != 0)
                                throw new InvalidDataException("Use non-interlaced 8-bit indexed, RGB or RGBA PNG, 1..256 pixels per side.");
                            header = true; break;
                        case "PLTE":
                            if (data || colours != null || size < 3 || size > 768 || size % 3 != 0) throw new InvalidDataException("Invalid PLTE.");
                            colours = new byte[size]; Buffer.BlockCopy(bytes, start, colours, 0, size); break;
                        case "tRNS":
                            // Require RGBA for truecolour transparency so the loader has one
                            // unambiguous alpha path rather than guessing a transparent RGB key.
                            if (data || alpha != null || type != 3 || colours == null || size < 1 || size > colours.Length / 3)
                                throw new InvalidDataException("Use palette alpha or RGBA transparency.");
                            alpha = new byte[size]; Buffer.BlockCopy(bytes, start, alpha, 0, size); break;
                        case "IDAT":
                            if (dataEnded || (type == 3 && colours == null)) throw new InvalidDataException("Invalid IDAT order.");
                            data = true; compressed.Write(bytes, start, size); break;
                        case "IEND":
                            if (!data || size != 0 || crcPos + 4 != bytes.Length) throw new InvalidDataException("Invalid IEND.");
                            end = true; break;
                        default:
                            if ((bytes[pos + 4] & 32) == 0 || name == "acTL") throw new InvalidDataException("Unsupported PNG chunk: " + name);
                            break; // Metadata does not change the stored pixel values or palette.
                    }
                    pos = crcPos + 4;
                }
                if (!end) throw new InvalidDataException("Missing PNG IEND.");
                // A PNG palette can be arbitrary. Reuse slots only when its complete
                // palette and alpha match DFU; otherwise preserve the authored colours.
                // This avoids silently treating a paint program's reordered slots as dyes.
                if (preserveIndexes)
                {
                    indexed = type == 3 && colours != null && colours.Length == 768 && palette != null && alpha != null && alpha.Length > 0 && alpha[0] == 0;
                    if (indexed)
                    {
                        for (int i=0;i<256;i++)
                            if (colours[i*3]!=palette.GetRed(i) || colours[i*3+1]!=palette.GetGreen(i) || colours[i*3+2]!=palette.GetBlue(i)) { indexed=false; break; }
                        for (int i=1;i<alpha.Length;i++) if(alpha[i]!=255) indexed=false;
                    }
                }
                if (indexed)
                {
                    if (type != 3 || colours == null || colours.Length != 768 || palette == null)
                        throw new InvalidDataException("Indexed PNG validation requires all 256 ART_PAL.COL slots.");
                    // DFPalette may retain an eight-byte COL header. Its accessors, not
                    // raw PaletteBuffer offsets, identify the RGB value of each slot.
                    for (int i = 0; i < 256; i++)
                        if (colours[i * 3] != palette.GetRed(i) || colours[i * 3 + 1] != palette.GetGreen(i) || colours[i * 3 + 2] != palette.GetBlue(i))
                            throw new InvalidDataException("PNG palette differs from ART_PAL.COL; preserve palette slot order.");
                    if (alpha == null || alpha[0] != 0) throw new InvalidDataException("Indexed PNG validation requires transparent index 0.");
                    for (int i = 1; i < alpha.Length; i++) if (alpha[i] != 255)
                        throw new InvalidDataException("Only index 0 may be transparent in a native indexed source.");
                }
                byte[] z = compressed.ToArray();
                if (z.Length < 6 || (z[0] & 15) != 8 || (z[0] >> 4) > 7 || ((z[0] << 8) + z[1]) % 31 != 0 || (z[1] & 32) != 0)
                    throw new InvalidDataException("Invalid PNG zlib header.");
                int bpp = type == 3 ? 1 : type == 2 ? 3 : 4;
                int stride = result.Width * bpp;
                byte[] filtered = new byte[(stride + 1) * result.Height];
                // Unity's Mono DeflateStream expects raw deflate: strip the zlib wrapper.
                using (var input = new MemoryStream(z, 2, z.Length - 6))
                using (var inflater = new DeflateStream(input, CompressionMode.Decompress))
                {
                    int count = 0, n;
                    while (count < filtered.Length && (n = inflater.Read(filtered, count, filtered.Length - count)) > 0) count += n;
                    if (count != filtered.Length || inflater.ReadByte() != -1) throw new InvalidDataException("PNG inflated size mismatch.");
                }
                uint s1 = 1, s2 = 0;
                foreach (byte value in filtered) { s1 = (s1 + value) % 65521; s2 = (s2 + s1) % 65521; }
                int tail = z.Length - 4;
                uint adler = ((uint)z[tail] << 24) | ((uint)z[tail + 1] << 16) | ((uint)z[tail + 2] << 8) | z[tail + 3];
                if ((s2 << 16 | s1) != adler) throw new InvalidDataException("PNG Adler32 mismatch.");
                var pixels = new byte[stride * result.Height];
                for (int y = 0; y < result.Height; y++)
                {
                    int filter = filtered[y * (stride + 1)];
                    if (filter > 4) throw new InvalidDataException("Unknown PNG row filter.");
                    for (int x = 0; x < stride; x++)
                    {
                        int p = y * stride + x;
                        int a = x >= bpp ? pixels[p - bpp] : 0, b = y > 0 ? pixels[p - stride] : 0;
                        int c = y > 0 && x >= bpp ? pixels[p - stride - bpp] : 0;
                        int predictor = filter == 0 ? 0 : filter == 1 ? a : filter == 2 ? b : filter == 3 ? (a + b) / 2 : Paeth(a, b, c);
                        pixels[p] = unchecked((byte)(filtered[y * (stride + 1) + x + 1] + predictor));
                    }
                }
                if (indexed)
                {
                    if (!preserveIndexes) foreach (byte value in pixels) if (value == 255) throw new InvalidDataException("Paperdoll mask index 255 is not allowed.");
                    result.Indexed = new DFBitmap(result.Width, result.Height) { Palette = palette };
                    Buffer.BlockCopy(pixels, 0, result.Indexed.Data, 0, pixels.Length);
                }
                else
                {
                    result.FixedPixels = new Color32[result.Width * result.Height];
                    for (int y = 0; y < result.Height; y++) for (int x = 0; x < result.Width; x++)
                    {
                        int p = y * stride + x * bpp;
                        Color32 colour;
                        if (type == 3)
                        {
                            int index = pixels[p];
                            if (colours == null || index * 3 + 2 >= colours.Length) throw new InvalidDataException("Missing PNG palette entry.");
                            colour = new Color32(colours[index * 3], colours[index * 3 + 1], colours[index * 3 + 2], alpha != null && index < alpha.Length ? alpha[index] : (byte)255);
                        }
                        else colour = new Color32(pixels[p], pixels[p + 1], pixels[p + 2], type == 6 ? pixels[p + 3] : (byte)255);
                        result.FixedPixels[(result.Height - 1 - y) * result.Width + x] = colour;
                    }
                }
                return result;
            }
        }
    }
}
