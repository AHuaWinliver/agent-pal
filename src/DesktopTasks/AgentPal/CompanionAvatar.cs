// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace AgentPal
{
    // One portrait source for conversations and the brand introduction.
    internal static class CompanionAvatar
    {
        internal static Image Load(string id, int size)
        {
            PetCharacter character=PetCharacter.Find(id);
            if(!character.IsExternal)
            {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AgentPal.Avatar."+id+".png"))
                    if(stream!=null)using(var source=Image.FromStream(stream))return Resize(source,new RectangleF(0,0,source.Width,source.Height),size,true);
            }
            string path=character.AvatarPath;
            if(!File.Exists(path))return null;
            using(Image source=Image.FromFile(path))
            {
                RectangleF crop=character.IsExternal?new RectangleF(0,0,source.Width,source.Height):
                    id=="pippin"?new RectangleF(50,180,165,165):new RectangleF(65,16,260,230);
                return Resize(source,crop,size,character.IsExternal);
            }
        }
        private static Image Resize(Image source,RectangleF crop,int size,bool contain)
        {
            var result=new Bitmap(size,size);
            using(Graphics g=Graphics.FromImage(result))
            {
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                RectangleF target=new RectangleF(0,0,size,size);
                if(contain){float scale=Math.Min(size/crop.Width,size/crop.Height);target=new RectangleF((size-crop.Width*scale)/2,(size-crop.Height*scale)/2,crop.Width*scale,crop.Height*scale);}
                g.DrawImage(source,target,crop,GraphicsUnit.Pixel);
            }
            return result;
        }
    }
}
