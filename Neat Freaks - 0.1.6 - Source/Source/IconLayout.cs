using System;
using System.IO;
using DaggerfallConnect;
using UnityEngine;

namespace NeatFreaks
{
    // Use a single coordinate convention to avoid flipped crops: PNG is top-down;
    // fixed RGBA storage is bottom-up, whereas indexed DFBitmap storage is top-down.
    // Copy pixels unchanged so cropping preserves the art and dye slots.
    public static class IconLayout
    {
        // Logical UI units only, NOT GPU resolution. A large proportional size lets
        // ItemListScroller's ScaleToFit shrink to its actual interior even with
        // MaxAutoScale=1. This avoids patching windows or tracking UI resolution.
        public const int DisplayMultiplier = 256;

        static int Position(PngIcon icon, int x, int y)
        { return (icon.Indexed != null ? y : icon.Height - 1 - y) * icon.Width + x; }
        static bool Visible(PngIcon icon, int x, int y)
        {
            int p = Position(icon,x,y);
            return icon.Indexed != null ? icon.Indexed.Data[p] != 0 : icon.FixedPixels[p].a != 0;
        }
        public static PngIcon Prepare(PngIcon source)
        {
            // Treat authored markers as ordinary pixels so cropping cannot move them. Every visible
            // pixel participates in bounds; artwork is neither detected nor moved.
            int w=source.Width,h=source.Height;
            int left=w,top=h,right=-1,bottom=-1;
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)
            {
                if(!Visible(source,x,y)) continue;
                left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
            }
            // Reject blank icons so the existing fallback/log-once
            // path displays the vanilla image rather than an invisible item.
            if(right<left) throw new InvalidDataException("Inventory icon contains no visible garment pixels.");
            int width=right-left+3,height=bottom-top+3;
            var result=new PngIcon { Width=width,Height=height };
            if(source.Indexed!=null) result.Indexed=new DFBitmap(width,height) { Palette=source.Indexed.Palette };
            else result.FixedPixels=new Color32[width*height];
            for(int y=top;y<=bottom;y++) for(int x=left;x<=right;x++)
            {
                int dest=Position(result,x-left+1,y-top+1),src=Position(source,x,y);
                if(result.Indexed!=null) result.Indexed.Data[dest]=source.Indexed.Data[src];
                else result.FixedPixels[dest]=source.FixedPixels[src];
            }
            return result;
        }
    }
}
