// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace AgentPal
{
    internal sealed partial class PetForm
    {
        private readonly Dictionary<int,Image> walkingExpressionPatches=new Dictionary<int,Image>();
        private readonly Dictionary<string,IntPtr> walkingExpressionCache=new Dictionary<string,IntPtr>();
        private readonly Queue<string> walkingExpressionCacheOrder=new Queue<string>();
        private readonly Stopwatch walkingExpressionTime=Stopwatch.StartNew();
        private WalkingExpressionClock walkingExpressionClock;
        private float[] walkingExpressionAttachment=new float[4];
        private const int WalkingExpressionCacheLimit=72;

        private void AdoptWalkingExpressions(Dictionary<int, Image> patches, string characterId,float[] attachment=null)
        {
            DisposeWalkingExpressions();
            foreach (var patch in patches) walkingExpressionPatches.Add(patch.Key, patch.Value);
            walkingExpressionClock=patches.Count == 6 ? new WalkingExpressionClock(unchecked(Environment.TickCount+characterId.Length*127),characterId) : null;
            walkingExpressionAttachment=attachment??new float[4];
            walkingExpressionTime.Restart();
        }
        private void DisposeWalkingExpressionCache()
        {
            foreach(IntPtr handle in walkingExpressionCache.Values)if(handle!=IntPtr.Zero)DeleteObject(handle);
            walkingExpressionCache.Clear();walkingExpressionCacheOrder.Clear();
        }
        private void DisposeWalkingExpressions()
        {
            DisposeWalkingExpressionCache();
            foreach(Image image in walkingExpressionPatches.Values)image.Dispose();walkingExpressionPatches.Clear();
            walkingExpressionClock=null;
        }
        private bool TryPushWalkingExpression(Image frame)
        {
            if(!IsLocomotionState(state)||reduceMotion||paused||walkingExpressionClock==null)return false;
            int blink,smile;walkingExpressionClock.Sample(walkingExpressionTime.Elapsed.TotalSeconds,out blink,out smile);
            if(blink==0&&smile==0)return false;
            return PushWalkingExpression(frame,blink,smile);
        }
        private bool PushWalkingExpression(Image frame,int blink,int smile)
        {
            string key=state+"|"+(frameIndex%24)+"|"+blink+"|"+smile;
            IntPtr handle;
            if(!walkingExpressionCache.TryGetValue(key,out handle))
            {
                using(Bitmap surface=RenderSpriteBitmap(frame,ClientSize))
                {
                    using(Graphics g=Graphics.FromImage(surface))
                    {
                    g.CompositingQuality=CompositingQuality.HighQuality;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    Rectangle box=FitSpriteRectangle(frame.Size,ClientSize);
                    g.TranslateTransform(box.X,box.Y);g.ScaleTransform(box.Width/384f,box.Height/416f);
                    ApplyWalkingExpressionTransform(g,currentCharacterId,state,frameIndex%24,walkingExpressionAttachment);
                    if(blink>0)g.DrawImage(walkingExpressionPatches[blink],0,0,384,416);
                    if(smile>0)g.DrawImage(walkingExpressionPatches[smile+3],0,0,384,416);
                    }
                    handle=surface.GetHbitmap(Color.FromArgb(0));
                }
                while(walkingExpressionCache.Count>=WalkingExpressionCacheLimit)
                {string old=walkingExpressionCacheOrder.Dequeue();DeleteObject(walkingExpressionCache[old]);walkingExpressionCache.Remove(old);}
                walkingExpressionCache.Add(key,handle);walkingExpressionCacheOrder.Enqueue(key);
            }
            PushLayeredHandle(handle,ClientSize);return true;
        }
        private static float[] ReadWalkingExpressionAttachment(string file)
        {
            if(!File.Exists(file))return new float[4]; // Older resource builds keep their original placement.
            if(new FileInfo(file).Length>2048)throw new InvalidDataException("Invalid expression attachment size.");
            var offsets=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<float[]>(File.ReadAllText(file));
            if(offsets==null||offsets.Length!=4)throw new InvalidDataException("Expression attachment requires four offsets.");
            foreach(float value in offsets)if(float.IsNaN(value)||float.IsInfinity(value)||Math.Abs(value)>100)throw new InvalidDataException("Invalid expression attachment offset.");
            return offsets;
        }
        internal static void ApplyWalkingFaceTransform(Graphics g,string id,string state,int frame)
        {ApplyWalkingExpressionTransform(g,id,state,frame,null);}
        internal static void ApplyWalkingExpressionTransform(Graphics g,string id,string state,int frame,float[] offsets)
        {
            double t=frame/24.0*Math.PI*2;
            if(CompanionMotion.MirrorWalkingFace(id,state)){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
            if(id!="pippin")
            {
                g.TranslateTransform((float)Math.Sin(t)*.9f,(float)Math.Cos(t*2)*.75f);
                g.TranslateTransform(192,260);g.RotateTransform((float)Math.Sin(t)*.45f);g.TranslateTransform(-192,-260);
            }
            if(offsets!=null)g.TranslateTransform(offsets[2],offsets[3]);
            CompanionMotion.ApplyWalkingNeck(g,id,frame/24.0);
            if(offsets!=null)g.TranslateTransform(offsets[0],offsets[1]);
        }
    }
}
