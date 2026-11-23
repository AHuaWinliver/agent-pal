// SPDX-License-Identifier: Apache-2.0
// Imports AI-authored component sheets into explicit, reviewable crop metadata.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Script.Serialization;

internal static class PrepareCompanionRig
{
    private static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==3&&args[0]=="--heads"){ImportHeads(args[1],args[2]);return 0;}
            if(args.Length==3&&args[0]=="--avatar")
            {using(var source=new Bitmap(args[1]))using(var image=new Bitmap(192,192,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(image)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Rectangle(0,0,192,192),new Rectangle(73,16,240,240),GraphicsUnit.Pixel);image.Save(args[2],ImageFormat.Png);}return 0;}
            if(args.Length<3||args.Length>4)throw new ArgumentException("Usage: PrepareCompanionRig normal-sheet side-sheet new-artwork-root [staff-and-cloud-sheet]");
            string root=Path.GetFullPath(args[2]);if(Directory.Exists(root))throw new IOException("Output already exists.");
            Import(args[0],Path.Combine(root,"rigs"));int[][] side=Import(args[1],Path.Combine(root,"side-rigs"));
            Directory.CreateDirectory(Path.Combine(root,"side-torsos"));
            using(var atlas=new Bitmap(args[1]))using(var torso=atlas.Clone(new Rectangle(side[1][0],side[1][1],side[1][2],side[1][3]),PixelFormat.Format32bppArgb))
            {torso.Save(Path.Combine(root,"side-torsos","orbit.png"),ImageFormat.Png);File.WriteAllText(Path.Combine(root,"side-torsos","regions.json"),Json.Serialize(new Dictionary<string,int[]>{{"orbit",new[]{0,0,torso.Width,torso.Height}}}));}
            if(args.Length==4)
            {
                using(var atlas=new Bitmap(args[3]))
                {
                    Rectangle staff=Bounds(atlas,new Rectangle(0,0,atlas.Width/2,atlas.Height)),cloud=Bounds(atlas,new Rectangle(atlas.Width/2,0,atlas.Width-atlas.Width/2,atlas.Height));
                    using(var pole=atlas.Clone(staff,PixelFormat.Format32bppArgb))pole.Save(Path.Combine(root,"staff.png"),ImageFormat.Png);
                    using(var platform=new Bitmap(512,256,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(platform))
                    {g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;float scale=Math.Min(488f/cloud.Width,230f/cloud.Height);float w=cloud.Width*scale,h=cloud.Height*scale;g.DrawImage(atlas,new RectangleF((512-w)/2,(256-h)/2,w,h),cloud,GraphicsUnit.Pixel);platform.Save(Path.Combine(root,"artifact-platform.png"),ImageFormat.Png);}
                }
            }
            Console.WriteLine("Imported nine normal and nine profile parts with explicit pixel bounds: "+root);return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void ImportHeads(string source,string output)
    {
        if(Directory.Exists(output))throw new IOException("Output already exists.");Directory.CreateDirectory(output);
        using(var atlas=new Bitmap(source))for(int i=0;i<6;i++)
        {
            int x=(int)Math.Round(i%3*atlas.Width/3.0),y=(int)Math.Round(i/3*atlas.Height/2.0),right=(int)Math.Round((i%3+1)*atlas.Width/3.0),bottom=(int)Math.Round((i/3+1)*atlas.Height/2.0);
            Rectangle b=Bounds(atlas,new Rectangle(x,y,right-x,bottom-y));
            using(var image=atlas.Clone(b,PixelFormat.Format32bppArgb)){image.RotateFlip(RotateFlipType.RotateNoneFlipX);image.Save(Path.Combine(output,(i<3?"normal-":"profile-")+new[]{"neutral","open","blink"}[i%3]+".png"),ImageFormat.Png);}
        }
        Console.WriteLine("Imported six upright heads; mirrored authored left-facing heads to match right-facing bodies: "+output);
    }
    private static int[][] Import(string source,string output)
    {
        Directory.CreateDirectory(output);var boxes=new int[9][];
        using(var atlas=new Bitmap(source))for(int i=0;i<9;i++)
        {
            int x=(int)Math.Round(i%3*atlas.Width/3.0),y=(int)Math.Round(i/3*atlas.Height/3.0),right=(int)Math.Round((i%3+1)*atlas.Width/3.0),bottom=(int)Math.Round((i/3+1)*atlas.Height/3.0);
            Rectangle b=Bounds(atlas,new Rectangle(x,y,right-x,bottom-y));boxes[i]=new[]{b.X,b.Y,b.Width,b.Height};
        }
        File.Copy(source,Path.Combine(output,"orbit.png"));File.WriteAllText(Path.Combine(output,"regions.json"),Json.Serialize(new Dictionary<string,int[][]>{{"orbit",boxes}}));return boxes;
    }
    private static Rectangle Bounds(Bitmap image,Rectangle cell)
    {
        int left=cell.Right,top=cell.Bottom,right=cell.Left,bottom=cell.Top;
        for(int y=cell.Top;y<cell.Bottom;y++)for(int x=cell.Left;x<cell.Right;x++)if(image.GetPixel(x,y).A>=64){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);}
        if(right<=left||bottom<=top)throw new InvalidDataException("Empty atlas cell: "+cell);
        return Rectangle.Intersect(cell,Rectangle.FromLTRB(left-2,top-2,right+2,bottom+2));
    }
}
