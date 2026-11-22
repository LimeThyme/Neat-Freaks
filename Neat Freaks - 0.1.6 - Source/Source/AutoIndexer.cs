using System;
using System.IO;
using System.Collections.Generic;
using DaggerfallConnect;
using UnityEngine;

namespace NeatFreaks
{
    // RGB loses palette identity, so conversion uses a predictable rule rather
    // than guessing dye intent. Ordinary colours use 1-254; exact mask-black is special.
    // Equal-distance ties choose the lowest permitted slot.
    public static class AutoIndexer
    {
        public static PngIcon Convert(PngIcon source, DFPalette palette, Action<string> log)
        {
            if(source.FixedPixels==null || palette==null) throw new InvalidDataException("Auto indexing requires decoded colours and the DFU palette.");
            var bitmap=new DFBitmap(source.Width,source.Height) { Palette=palette };
            var matches=new Dictionary<long,byte>();
            int transparent=0,exact=0,nearest=0,dyePixels=0,metalPixels=0,partial=0,maskPixels=0;
            for(int y=0;y<source.Height;y++) for(int x=0;x<source.Width;x++)
            {
                int p=(source.Height-1-y)*source.Width+x;
                Color32 c=source.FixedPixels[p];
                if(c.a!=0 && c.a!=255) partial++;
                // Indexed DFU textures have binary alpha; threshold is explicit.
                if(c.a<128) { transparent++;continue; }
                // preserve exact paperdoll-mask colour BEFORE normal RGB
                // matching. Slot 0 and 255 share black in ART_PAL.COL; alpha
                // distinguishes transparency from an opaque mask in exported PNGs.
                // Never nearest-match to 255: near-black garment shading must survive.
                if(c.r==palette.GetRed(255) && c.g==palette.GetGreen(255) && c.b==palette.GetBlue(255))
                {
                    bitmap.Data[y*source.Width+x]=255; maskPixels++; continue;
                }
                long key=c.r | ((long)c.g<<8) | ((long)c.b<<16);
                byte chosen;
                if(!matches.TryGetValue(key,out chosen))
                {
                    int best=int.MaxValue;chosen=1;
                    for(int i=1;i<255;i++)
                    {
                        int dr=c.r-palette.GetRed(i),dg=c.g-palette.GetGreen(i),db=c.b-palette.GetBlue(i);
                        int distance=dr*dr+dg*dg+db*db;
                        if(distance<best) {best=distance;chosen=(byte)i;}
                        if(distance==0) break;
                    }
                    matches.Add(key,chosen);
                }
                if(c.r==palette.GetRed(chosen)&&c.g==palette.GetGreen(chosen)&&c.b==palette.GetBlue(chosen)) exact++;else nearest++;
                if(chosen>=96 && chosen<=111)dyePixels++;
                if(chosen>=112 && chosen<=127)metalPixels++;
                bitmap.Data[y*source.Width+x]=chosen;
            }
            log("Auto-index complete: exact="+exact+"; nearest="+nearest+"; transparent="+transparent+"; mask-pixels="+maskPixels+"; partial-alpha="+partial+"; dye-pixels="+dyePixels+"; metal-slot-pixels="+metalPixels+"; cached until reload. RGB ties use lowest slot; alpha threshold=128.");
            return new PngIcon {Width=source.Width,Height=source.Height,Indexed=bitmap};
        }
    }
}
