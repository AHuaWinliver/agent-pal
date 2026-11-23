// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace AgentPal
{
    internal static class QuickTaskIcons
    {
        [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
        internal static Icon CreateWindowIcon(Image portrait)
        {
            using(var bitmap=new Bitmap(32,32,PixelFormat.Format32bppArgb))
            {
                using(var graphics=Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(portrait,new Rectangle(0,0,32,32));
                }
                IntPtr handle=bitmap.GetHicon();
                try{using(var borrowed=Icon.FromHandle(handle))return (Icon)borrowed.Clone();}
                finally{DestroyIcon(handle);}
            }
        }
        internal static void Write(Image character,string path)
        {
            // Use the taskbar's existing Q-style portrait, background and CLI badge.
            using(Bitmap portrait=AppIcon.CreateBitmap(character,128))
            {
                portrait.Save(Path.ChangeExtension(path,".png"),ImageFormat.Png);
                int[] sizes={16,24,32,48,64,128};var images=new List<byte[]>();
                foreach(int size in sizes)
                using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb))
                using(var graphics=Graphics.FromImage(bitmap))
                using(var buffer=new MemoryStream())
                {
                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    graphics.DrawImage(portrait,new Rectangle(0,0,size,size));bitmap.Save(buffer,ImageFormat.Png);images.Add(buffer.ToArray());
                }
                // PNG-backed ICO avoids Icon.Save's low-color GDI serialization.
                using(var file=File.Create(path))using(var writer=new BinaryWriter(file))
                {
                    writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
                    int offset=6+16*sizes.Length;
                    for(int i=0;i<sizes.Length;i++)
                    {writer.Write((byte)sizes[i]);writer.Write((byte)sizes[i]);writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
                    foreach(byte[] image in images)writer.Write(image);
                }
            }
        }
        internal static Image ReadThumbnail(string iconPath)
        {
            if(String.IsNullOrEmpty(iconPath))return null;
            string png=Path.ChangeExtension(iconPath,".png");
            if(File.Exists(png))using(Image image=Image.FromFile(png))return new Bitmap(image);
            if(File.Exists(iconPath))using(var icon=new Icon(iconPath))return icon.ToBitmap();
            return null;
        }
    }
}
