// SPDX-License-Identifier: Apache-2.0
// Offline export using the same reviewed human gait and limb mesh as built-ins.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using AgentPal;

internal static partial class BuildOriginalCharacters
{
    private static Bitmap customStaff;
    private static bool customTail;
    private static string customCharacterId;
    private static PointF customMouth;
    private static Bitmap customWaveArm;
    private static readonly Dictionary<Bitmap,CustomArmArt> CustomArms=new Dictionary<Bitmap,CustomArmArt>();
    private static readonly List<object> CustomArmAudit=new List<object>();
    private static readonly List<object> WaveArmAudit=new List<object>();
    private static string customState;private static int customFrame;
    private static bool customTorsoDrawn;
    private static PointF[] customNormalShoulders;
    private static string proportionPreviewId;
    private static float proportionPreviewScale=1;
    private const float ProportionHeadLift=8;
    private sealed class AnatomyView
    {
        public float[] root {get;set;} public float[] collar {get;set;} public float[] opening {get;set;} public float[] skin {get;set;} public float[] torsoAnchorUv {get;set;}
        public float lift {get;set;} public float width {get;set;} public float topOffset {get;set;} public float feather {get;set;} public float burialY {get;set;} public bool originalNeck {get;set;} public float extensionTop {get;set;}
        public float[][] aperture {get;set;} public float[][] headNeckPolygon {get;set;}
    }
    private sealed class AnatomyCalibration
    {
        public float armScale {get;set;}
        public float[][] shoulders {get;set;} public float[][] walkingShoulders {get;set;}
        public AnatomyView normal {get;set;} public AnatomyView profile {get;set;}
        public float normalTorsoThickness {get;set;} public float profileTorsoThickness {get;set;}
    }
    private static Dictionary<string,AnatomyCalibration> Anatomy=new Dictionary<string,AnatomyCalibration>();
    private static bool anatomyDisabled;
    private static void LoadAnatomy(string artwork)
    {
        string file=Path.Combine(artwork,"anatomy-calibration.json");
        if(!File.Exists(file))throw new FileNotFoundException("The reviewed anatomy calibration is required.",file);
        Anatomy=new JavaScriptSerializer().Deserialize<Dictionary<string,AnatomyCalibration>>(File.ReadAllText(file));
        foreach(var entry in Anatomy)
        {
            var c=entry.Value;if(c==null)throw new InvalidDataException("Missing anatomy: "+entry.Key);
            if(entry.Key=="pippin")continue;
            if(c.armScale<.75f||c.armScale>1||c.shoulders==null||c.shoulders.Length!=2||c.normal==null||c.profile==null)
                throw new InvalidDataException("Invalid reviewed anatomy: "+entry.Key);
            foreach(var view in new[]{c.normal,c.profile})
                if(view.root==null||view.root.Length!=2||view.collar==null||view.collar.Length!=2||view.opening==null||view.opening.Length!=2||view.width<8||view.width>40||view.aperture==null||view.aperture.Length<3)
                    throw new InvalidDataException("Invalid neck/collar calibration: "+entry.Key);
        }
    }
    private static AnatomyCalibration Calibration(string id)
    {
        AnatomyCalibration value;
        return !anatomyDisabled&&Anatomy.TryGetValue(customCharacterId??id,out value)&&value.normal!=null?value:null;
    }
    private static void StabilizeRefinedHeadExpressions(Bitmap[] parts,bool profile,string id)
    {
        Bitmap neutral=parts[0];AnatomyCalibration calibration;
        Anatomy.TryGetValue(id,out calibration);
        var view=calibration==null?null:profile?calibration.profile:calibration.normal;
        using(var neck=new Bitmap(neutral.Width,neutral.Height))
        {
            using(var paint=Graphics.FromImage(neck))
            {
                if(view!=null&&view.headNeckPolygon!=null)
                {
                    PointF[] points=Array.ConvertAll(view.headNeckPolygon,delegate(float[] p){return new PointF(p[0]*neutral.Width,p[1]*neutral.Height);});
                    paint.FillPolygon(Brushes.White,points);
                }
                else paint.FillRectangle(Brushes.White,0,neutral.Height*.9f,neutral.Width,neutral.Height*.1f);
            }
            foreach(int slot in new[]{6,7})
            {
                var aligned=new Bitmap(neutral.Width,neutral.Height,PixelFormat.Format32bppArgb);
                using(var paint=Graphics.FromImage(aligned)){Quality(paint);DrawTexture(paint,parts[slot],new RectangleF(0,0,aligned.Width,aligned.Height));}
                for(int y=0;y<aligned.Height;y++)for(int x=0;x<aligned.Width;x++)
                {
                    Color original=neutral.GetPixel(x,y),expression=aligned.GetPixel(x,y);
                    bool preserve=original.A<240||expression.A<240||y>=neutral.Height*.9f||neck.GetPixel(x,y).A!=0;
                    aligned.SetPixel(x,y,preserve?original:Color.FromArgb(original.A,expression.R,expression.G,expression.B));
                }
                parts[slot].Dispose();parts[slot]=aligned;
            }
        }
    }
    private static bool HasAnatomy(string id){return Calibration(id)!=null;}
    private static PointF Point(float[] xy){return new PointF(xy[0],xy[1]);}
    private static float PreviewArmScale(string id){var c=Calibration(id);return id==proportionPreviewId?proportionPreviewScale:c==null?1:c.armScale;}
    private static PointF CalibratedShoulder(string id,int arm)
    {var c=Calibration(id);return c==null?CompanionMotion.ShoulderAnchor(id,arm==0):Point(c.shoulders[arm]);}
    internal static bool AttachmentSamples;
    private static bool RenderState(string state){return !AttachmentSamples||state=="idle"||state=="running-left"||state=="running-right"||state=="waving";}
    private static bool RenderFrame(int frame){return !AttachmentSamples||frame==0||frame==12||frame==20||frame==23;}
    private static PointF CalibratedWalkingShoulder(string id,int arm)
    {var c=Calibration(id);return c!=null&&c.walkingShoulders!=null?Point(c.walkingShoulders[arm]):CompanionMotion.WalkingShoulderAnchor(id,arm==0);}
    private static GraphicsPath HeadNeckPath(AnatomyView view,RectangleF box)
    {
        var path=new GraphicsPath();var points=Array.ConvertAll(view.headNeckPolygon,delegate(float[] xy){return new PointF(box.X+xy[0]*box.Width,box.Y+xy[1]*box.Height);});
        path.AddPolygon(points);return path;
    }
    private static float[] WalkingExpressionOffsets(string id)
    {
        var c=Calibration(id);if(c==null)return new float[4];
        var view=c.profile;PointF runtime=CompanionMotion.WalkingNeckAnchor(id);
        return new[]{runtime.X-view.root[0],runtime.Y-view.root[1]-view.lift,view.collar[0]-runtime.X,view.collar[1]-runtime.Y};
    }
    private static void DrawProportionHeadLayer(Graphics g,Rig rig,string id,string state,double phase,int expression,bool neckOnly)
    {
        // Split only the narrow cervical area, not the jaw or hanging hair.
        // Both passes inherit the same head transform; the real torso sits
        // between them and naturally covers the lower neck inside its collar.
        var c=Calibration(id);AnatomyView view=rig.Profile?c.profile:c.normal;
        PointF root=Point(view.root),collar=Point(view.collar);
        PointF neckBase=new PointF(root.X,root.Y+view.lift);
        PointF[] expected={collar};using(Matrix parent=g.Transform)parent.TransformPoints(expected);
        PointF opening=Point(view.opening);
        PointF[] buriedEdge={new PointF(opening.X-view.width/2,view.burialY>0?view.burialY:opening.Y+6)};using(Matrix parent=g.Transform)parent.TransformPoints(buriedEdge);
        var saved=g.Save();CompanionMotion.ApplyHeadAttachment(g,customCharacterId??id,state,phase);
        PointF[] projectedBase={neckBase};using(Matrix pose=g.Transform)pose.TransformPoints(projectedBase);
        g.TranslateTransform(expected[0].X-projectedBase[0].X,expected[0].Y-projectedBase[0].Y,MatrixOrder.Append);
        PointF[] actual={neckBase};using(Matrix attachment=g.Transform)attachment.TransformPoints(actual);
        if(Math.Abs(actual[0].X-expected[0].X)>.001f||Math.Abs(actual[0].Y-expected[0].Y)>.001f)
            throw new InvalidDataException("Calibrated lower neck base is not centered in the actual garment collar.");
        if(!neckOnly&&view.headNeckPolygon!=null)
        {
            using(var cut=HeadNeckPath(view,HeadBox(id,rig)))g.SetClip(cut,CombineMode.Exclude);
            DrawAttachedHead(g,rig,id,expression);g.Restore(saved);return;
        }
        using(var neck=new GraphicsPath())
        {
            float width=view.width,half=width/2,top=root.Y-view.topOffset,right=root.X+half;
            neck.AddRectangle(new RectangleF(root.X-half,top,width,48));
            if(neckOnly)
            {
                if(view.headNeckPolygon!=null)using(var cut=HeadNeckPath(view,HeadBox(id,rig)))g.SetClip(cut,CombineMode.Intersect);
                else g.SetClip(new RectangleF(root.X-half-1,top-1,width+2,50),CombineMode.Intersect);
                // Replace the detached cut edge with a continuation of the
                // existing painted skin, not a flat fill or an extra collar.
                // The garment later hides the buried end of this bridge.
                using(Matrix inverse=g.Transform){inverse.Invert();inverse.TransformPoints(buriedEdge);}
                Rig normal=Rigs[id];RectangleF box=HeadBox(id,normal);Bitmap skin=normal.Parts[0];
                PointF normalRoot=CompanionMotion.NeckAnchor(id);
                var source=new RectangleF((normalRoot.X-9-box.X)/box.Width*skin.Width,(normalRoot.Y-8-box.Y)/box.Height*skin.Height,18/box.Width*skin.Width,5/box.Height*skin.Height);
                if(view.skin!=null&&view.skin.Length==4)
                {
                    // Match the side painting's warmer neck shade. The normal
                    // view sample creates a pale stripe at the foreground join.
                    skin=rig.Parts[0];source=new RectangleF(skin.Width*view.skin[0],skin.Height*view.skin[1],skin.Width*view.skin[2],skin.Height*view.skin[3]);
                }
                using(var opaqueSkin=new ImageAttributes())
                {
                    var alpha=new ColorMatrix();alpha.Matrix33=1.05f;opaqueSkin.SetColorMatrix(alpha);
                    float extensionTop=view.originalNeck?view.extensionTop:top-1;
                    if(extensionTop<buriedEdge[0].Y+1)
                        g.DrawImage(skin,new[]{new PointF(root.X-half-1,extensionTop),new PointF(right+1,extensionTop),new PointF(buriedEdge[0].X-1,buriedEdge[0].Y+1)},source,GraphicsUnit.Pixel,opaqueSkin);
                    // Other identities already contain a clean painted neck.
                    // Keep it intact and extend only its buried end. Do not
                    // replace the whole visible neck with a stretched swatch.
                    if(view.originalNeck)DrawAttachedHead(g,rig,id,expression);
                }
            }
            else
            {
                var tile=new RectangleF(root.X-half-2,top-2,width+4,52);
                var outside=g.Save();g.SetClip(tile,CombineMode.Exclude);DrawAttachedHead(g,rig,id,expression);g.Restore(outside);
                using(var image=BuildFeatheredPreviewTile(tile,delegate(Graphics paint){DrawAttachedHead(paint,rig,id,expression);},delegate(float x,float y){
                    float inside=Math.Min(Math.Min(x-(root.X-half),right-x),Math.Min(y-top,top+48-y));
                    return 1-(float)CompanionMotion.Smooth(inside/(view.feather>0?view.feather:rig.Profile?2.0:1.0));
                }))g.DrawImage(image,tile);
            }
        }
        g.Restore(saved);
    }
    private static readonly Dictionary<Bitmap,Bitmap> TorsoThicknessCache=new Dictionary<Bitmap,Bitmap>();
    private static void DrawTorsoTexture(Graphics g,Bitmap torso,RectangleF box,string id,bool profile)
    {
        var c=Calibration(id);float gain=c==null?0:profile?c.profileTorsoThickness:c.normalTorsoThickness;
        if(gain==0){DrawTexture(g,torso,box);return;}
        Bitmap cache;
        if(!TorsoThicknessCache.TryGetValue(torso,out cache))
        {
            cache=new Bitmap(768,832,PixelFormat.Format32bppArgb);
            float axis=profile?CompanionMotion.WalkingNeckAnchor(id).X:c.normal.collar[0];
            using(var paint=Graphics.FromImage(cache))
            {
                Quality(paint);paint.ScaleTransform(2,2);
                for(int y=(int)Math.Floor(box.Top);y<(int)Math.Ceiling(box.Bottom);y++)
                {
                    float volume=(float)(CompanionMotion.Smooth((y+.5-206)/20)*(1-CompanionMotion.Smooth((y+.5-320)/48)));
                    float scale=1+gain*volume;var row=paint.Save();paint.SetClip(new RectangleF(0,y,384,1));
                    DrawTexture(paint,torso,new RectangleF(axis+(box.Left-axis)*scale,box.Y,box.Width*scale,box.Height));paint.Restore(row);
                }
            }
            TorsoThicknessCache.Add(torso,cache);
        }
        g.DrawImage(cache,new RectangleF(0,0,384,416));
    }
    private static void DisposeTorsoThickness()
    {foreach(var image in TorsoThicknessCache.Values)image.Dispose();TorsoThicknessCache.Clear();}
    private static void DrawProportionTorso(Graphics g,Bitmap torso,string id)
    {DrawProportionTorso(g,torso,new RectangleF(86,191,212,177),false,id);}
    private static void DrawProportionTorso(Graphics g,Bitmap torso,RectangleF box,bool profile,string id)
    {
        var saved=g.Save();
        // The body atlas has its own painted neck stump. Open only that skin
        // triangle; preserve both original lapel edges and the crossed collar.
        using(var aperture=new GraphicsPath())
        {
            var c=Calibration(id);var view=profile?c.profile:c.normal;
            PointF[] source=Array.ConvertAll(view.aperture,delegate(float[] xy){return Point(xy);});
            for(int i=0;i<source.Length;i++)source[i]=new PointF(box.X+source[i].X*box.Width,box.Y+source[i].Y*box.Height);
            aperture.AddPolygon(source);
            RectangleF bounds=aperture.GetBounds();var tile=new RectangleF((float)Math.Floor(bounds.Left-2),(float)Math.Floor(bounds.Top-2),(float)Math.Ceiling(bounds.Right+2)-(float)Math.Floor(bounds.Left-2),(float)Math.Ceiling(bounds.Bottom+2)-(float)Math.Floor(bounds.Top-2));
            g.SetClip(tile,CombineMode.Exclude);DrawTorsoTexture(g,torso,box,id,profile);g.Restore(saved);
            using(var image=BuildFeatheredPreviewTile(tile,delegate(Graphics paint){DrawTorsoTexture(paint,torso,box,id,profile);},delegate(float x,float y){
                bool inside=aperture.IsVisible(x,y);
                float distance=float.MaxValue;
                for(int i=0;i<source.Length;i++)
                {
                    PointF a=source[i],b=source[(i+1)%source.Length];float dx=b.X-a.X,dy=b.Y-a.Y;
                    float t=Math.Max(0,Math.Min(1,((x-a.X)*dx+(y-a.Y)*dy)/(dx*dx+dy*dy)));
                    float px=x-a.X-t*dx,py=y-a.Y-t*dy;distance=Math.Min(distance,(float)Math.Sqrt(px*px+py*py));
                }
                return inside?0:(float)CompanionMotion.Smooth(distance/.65);
            }))g.DrawImage(image,tile);
        }
    }
    private static Bitmap BuildFeatheredPreviewTile(RectangleF bounds,Action<Graphics> draw,Func<float,float,float> coverage)
    {
        var image=new Bitmap((int)(bounds.Width*2),(int)(bounds.Height*2),PixelFormat.Format32bppArgb);
        using(var paint=Graphics.FromImage(image)){Quality(paint);paint.ScaleTransform(2,2);paint.TranslateTransform(-bounds.X,-bounds.Y);draw(paint);}
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
        {
            Color c=image.GetPixel(x,y);float keep=Math.Max(0,Math.Min(1,coverage(bounds.X+(x+.5f)/2,bounds.Y+(y+.5f)/2)));
            image.SetPixel(x,y,Color.FromArgb((int)Math.Round(c.A*keep),c.R,c.G,c.B));
        }
        return image;
    }
    internal static void ExportProportionReview(string id,string artwork,string output,float scale)
    {
        if(id!="orbit")throw new ArgumentException("Confirm Qingheng proportions before applying them to other companions.");
        if(scale<.75f||scale>1)throw new ArgumentOutOfRangeException("scale");
        if(Directory.Exists(output))throw new IOException("Choose a new review directory.");
        var json=new JavaScriptSerializer();LoadAnatomy(artwork);
        try
        {
            foreach(string view in new[]{"rigs","side-rigs"})
            {
                string folder=Path.Combine(artwork,view);var boxes=json.Deserialize<Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(folder,"regions.json")));
                (view=="rigs"?Rigs:SideRigs).Add(id,new Rig(Path.Combine(folder,id+".png"),boxes[id]));
            }
            foreach(string variant in new[]{"before","candidate"})
            {
                anatomyDisabled=variant=="before";proportionPreviewId=variant=="candidate"?id:null;proportionPreviewScale=variant=="candidate"?scale:1;
                string sleeve=Path.Combine(artwork,"arm-refinements",id,"candidate-normal-near.png");
                if(variant=="candidate"&&File.Exists(sleeve))using(var source=new Bitmap(sleeve))
                {Rigs[id].Parts[3].Dispose();Rigs[id].Parts[3]=new Bitmap(source);Rigs[id].ArmAttachments[1]=OpaqueRoot(Rigs[id].Parts[3]);Rigs[id].ArmGrips[1]=new PointF(.60f,.66f);}
                foreach(string state in new[]{"idle","running-left","running-right"})
                {
                    string folder=Path.Combine(output,variant,state);Directory.CreateDirectory(folder);
                    for(int frame=0;frame<FrameCount(state);frame++)using(var image=Frame(id,state,frame/(double)FrameCount(state)))image.Save(Path.Combine(folder,frame.ToString("00")+".png"),ImageFormat.Png);
                }
                foreach(var arm in CustomArms.Values)arm.Dispose();CustomArms.Clear();
                if(variant=="candidate")using(var body=new Bitmap(384,416,PixelFormat.Format32bppArgb))using(var paint=Graphics.FromImage(body))
                {
                    Quality(paint);DrawProportionTorso(paint,SideRigs[id].Parts[1],ProfileTorsoBox(SideRigs[id],id),true,id);
                    body.Save(Path.Combine(output,"profile-body-layer.png"),ImageFormat.Png);
                    paint.Clear(Color.Transparent);DrawProportionTorso(paint,Rigs[id].Parts[1],id);
                    body.Save(Path.Combine(output,"normal-body-layer.png"),ImageFormat.Png);
                    paint.Clear(Color.Transparent);DrawTexture(paint,SideRigs[id].Parts[1],ProfileTorsoBox(SideRigs[id],id));
                    body.Save(Path.Combine(output,"profile-body-before.png"),ImageFormat.Png);
                }
            }
            File.WriteAllText(Path.Combine(output,"proportion-review.json"),json.Serialize(new{id=id,status="awaiting visual approval",armScale=scale,bodyCenterX=192,anatomicalRightShoulder=new[]{160,217},anatomicalLeftShoulder=new[]{224,217},palms="equal-height inward offsets of +/-4*scale",normalBuriedAnchor=new[]{192,207},normalCollarOpening=new[]{187,197},headNeckLift=ProportionHeadLift,foregroundBody="clothing and collar only; source neck removed from normal and profile",neckLayers="original neck skin continues into collar; lower end buried behind garment; no exposed source cut edge",leftSleeve="frontal flare narrowed with separately authored candidate; hand pose and palm anchor retained",unchanged="approved head height, head artwork, arms, legs and garment design; collar opening skin seam corrected",rightArm="attachment moved inward; elbow outward; palm slightly toward torso",scope="Qingheng only; no change to other companions or bundled production frames"}));
            Console.WriteLine("Created isolated Qingheng proportion review: "+output);
        }
        finally
        {
            proportionPreviewId=null;proportionPreviewScale=1;anatomyDisabled=false;
            foreach(var arm in CustomArms.Values)arm.Dispose();CustomArms.Clear();
            foreach(var rig in Rigs.Values)rig.Dispose();foreach(var rig in SideRigs.Values)rig.Dispose();Rigs.Clear();SideRigs.Clear();
        }
    }
    internal static void ExportCustom(string artwork,string profileFile,string output)
    {
        if(Directory.Exists(output))throw new IOException("Choose a new output directory; existing packs are never overwritten.");
        LoadAnatomy(Path.GetFullPath(Path.Combine(artwork,"..","..")));
        var json=new JavaScriptSerializer();var profile=json.Deserialize<CharacterPackManifest>(File.ReadAllText(profileFile));
        if(!CharacterPackCatalog.ValidId(profile.id))throw new ArgumentException("Invalid companion ID.");
        string normal=Path.Combine(artwork,"rigs"),side=Path.Combine(artwork,"side-rigs");
        var normalBoxes=json.Deserialize<Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(normal,"regions.json")));
        var sideBoxes=json.Deserialize<Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(side,"regions.json")));
        try
        {
            Rigs.Add("orbit",new Rig(Path.Combine(normal,"orbit.png"),normalBoxes["orbit"]));SideRigs.Add("orbit",new Rig(Path.Combine(side,"orbit.png"),sideBoxes["orbit"]));customTail=true;customCharacterId=profile.id;customMouth=new PointF(profile.mouthX*384,profile.mouthY*416);
            var layout=json.Deserialize<CustomArmLayout>(File.ReadAllText(Path.Combine(artwork,"arm-layout.json")));
            customNormalShoulders=new[]{new PointF(layout.normalShoulders[0][0],layout.normalShoulders[0][1]),new PointF(layout.normalShoulders[1][0],layout.normalShoulders[1][1])};
            foreach(var rig in new[]{Rigs["orbit"],SideRigs["orbit"]})for(int arm=0;arm<2;arm++)
            {
                var spec=(rig.Profile?layout.profile:layout.normal)[arm];Bitmap source=rig.Parts[arm+2];
                var bounds=new Rectangle(0,0,source.Width,spec.height>0?spec.height:source.Height);
                Bitmap clean=source.Clone(bounds,PixelFormat.Format32bppArgb);if(spec.flipX)clean.RotateFlip(RotateFlipType.RotateNoneFlipX);
                source.Dispose();rig.Parts[arm+2]=clean;rig.ArmAttachments[arm]=OpaqueRoot(clean);rig.ArmGrips[arm]=HandGrip(clean,false);
            }
            if(HasAnatomy("orbit"))customNormalShoulders=new[]{CalibratedShoulder("orbit",0),CalibratedShoulder("orbit",1)};
            ApplyCustomHandRefinement(artwork,Rigs["orbit"]);
            if(profile.id=="bailong")
            {
                foreach(int slot in new[]{0,6,7})CleanLowerHeadMatte(SideRigs["orbit"].Parts[slot],-1);
                CleanLowerHeadMatte(Rigs["orbit"].Parts[1],-1);CleanLowerHeadMatte(SideRigs["orbit"].Parts[1],-1);
            }
            string wave=Path.Combine(artwork,"wave-arm.png");
            if(File.Exists(wave))
            {
                customWaveArm=new Bitmap(wave);customWaveArm.RotateFlip(RotateFlipType.RotateNoneFlipX);
                CustomArms.Add(customWaveArm,new CustomArmArt(customWaveArm,OpaqueRoot(customWaveArm),new PointF(.43f,.79f),100*PreviewArmScale("orbit")));
            }
            string staff=Path.Combine(artwork,"staff.png");if(File.Exists(staff))customStaff=new Bitmap(staff);
            Directory.CreateDirectory(output);profile.files=new Dictionary<string,string>();profile.schemaVersion=1;profile.canvasWidth=384;profile.canvasHeight=416;
            foreach(string state in States)
            {
                if(!RenderState(state))continue;
                string directory=Path.Combine(output,state);Directory.CreateDirectory(directory);
                for(int frame=0;frame<FrameCount(state);frame++)
                {
                    if(!RenderFrame(frame))continue;
                    customState=state;customFrame=frame;customTorsoDrawn=false;
                    string relative=state+"/"+frame.ToString("00")+".png",file=Path.Combine(output,relative);
                    using(var image=Frame("orbit",state,frame/(double)FrameCount(state)))image.Save(file,ImageFormat.Png);
                    profile.files[relative]=FileHash(file);
                }
            }
            ExportAnatomyLayers("orbit",Path.GetDirectoryName(output));
            string platform=Path.Combine(artwork,"artifact-platform.png");
            if(File.Exists(platform)){File.Copy(platform,Path.Combine(output,"artifact-platform.png"));profile.files["artifact-platform.png"]=FileHash(platform);}
            File.WriteAllText(Path.Combine(output,AttachmentSamples?"REVIEW-ONLY.json":"character.json"),json.Serialize(profile));
            File.WriteAllText(output+".arm-audit.json",json.Serialize(CustomArmAudit));
            File.WriteAllText(output+".wave-audit.json",json.Serialize(WaveArmAudit));
            Console.WriteLine("Exported "+profile.id+(AttachmentSamples?": attachment review samples only.":": 240 transparent frames from newly drawn layered artwork."));
        }
        finally
        {
            foreach(var arm in CustomArms.Values)arm.Dispose();CustomArms.Clear();DisposeTorsoThickness();
            CustomArmAudit.Clear();WaveArmAudit.Clear();
            customNormalShoulders=null;
            foreach(var rig in Rigs.Values)rig.Dispose();foreach(var rig in SideRigs.Values)rig.Dispose();Rigs.Clear();SideRigs.Clear();customTail=false;customCharacterId=null;
            if(customStaff!=null){customStaff.Dispose();customStaff=null;}
            if(customWaveArm!=null){customWaveArm.Dispose();customWaveArm=null;}
        }
    }
    private sealed class HandRefinement
    {public int[] target {get;set;}public int originalCuffBottom {get;set;}}
    private static void ApplyCustomHandRefinement(string artwork,Rig rig)
    {
        string directory=Path.GetFullPath(Path.Combine(artwork,"..","..","arm-refinements",customCharacterId));
        string file=Path.Combine(directory,"normal-near-hand.png");if(!File.Exists(file))return;
        var spec=new JavaScriptSerializer().Deserialize<HandRefinement>(File.ReadAllText(Path.Combine(directory,"normal-near-hand.json")));
        Bitmap original=rig.Parts[3],revised=new Bitmap(original.Width,original.Height,PixelFormat.Format32bppArgb);
        using(var hand=new Bitmap(file))using(var g=Graphics.FromImage(revised))
        {
            Quality(g);g.DrawImage(hand,new Rectangle(spec.target[0],spec.target[1],spec.target[2],spec.target[3]));
            g.SetClip(new Rectangle(0,0,original.Width,spec.originalCuffBottom));g.DrawImageUnscaled(original,0,0);
        }
        rig.Parts[3]=revised;original.Dispose();rig.ArmAttachments[1]=OpaqueRoot(revised);rig.ArmGrips[1]=HandGrip(revised,false);
    }
    private static void CleanLowerHeadMatte(Bitmap image,float start=.70f)
    {
        // Remove low-alpha background haze around the existing lower silhouette.
        // RGB, opaque character pixels and upper hair remain untouched.
        int w=image.Width,h=image.Height;float[] distance=new float[w*h];
        var data=image.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        byte[] bytes=new byte[data.Stride*h];System.Runtime.InteropServices.Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
        try
        {
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)distance[y*w+x]=bytes[y*data.Stride+x*4+3]>=210?0:10000;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {int p=y*w+x;if(x>0)distance[p]=Math.Min(distance[p],distance[p-1]+1);if(y>0)distance[p]=Math.Min(distance[p],distance[p-w]+1);}
            for(int y=h-1;y>=0;y--)for(int x=w-1;x>=0;x--)
            {int p=y*w+x;if(x+1<w)distance[p]=Math.Min(distance[p],distance[p+1]+1);if(y+1<h)distance[p]=Math.Min(distance[p],distance[p+w]+1);}
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float strength=start<0?1:(float)CompanionMotion.Smooth((y/(float)h-start)/.06),keep=(float)CompanionMotion.Smooth((4-distance[y*w+x])/3);
                int p=y*data.Stride+x*4+3;bytes[p]=(byte)Math.Round(bytes[p]*(1-strength+strength*keep));
            }
            System.Runtime.InteropServices.Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
        }
        finally{image.UnlockBits(data);}
    }
    private static string FileHash(string file){using(var hash=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
    private static void DrawCustomTail(Graphics g,Rig rig,string state,double phase)
    {
        if(!customTail)return;var saved=g.Save();
        if(state=="jumping")g.TranslateTransform(0,(float)(-15*Math.Pow(Math.Sin(Math.PI*phase),2)));
        g.TranslateTransform(153,306);g.RotateTransform((float)Math.Sin(phase*Math.PI*2)*5);g.TranslateTransform(-153,-306);
        DrawTexture(g,rig.Parts[8],new RectangleF(55,270,115,100));g.Restore(saved);
    }
    private static void DrawCustomStaff(Graphics g,Rig rig,string id,double phase)
    {
        // One controlling hand rolls the staff; the free hand releases and
        // supports it only while the shaft passes its reachable grip. Two
        // permanent, opposing grips would force the wrists to cross every turn.
        double angle=phase*Math.PI*2;var center=new PointF(244+(float)Math.Sin(angle)*2,285+(float)Math.Cos(angle));
        // A tilted twirling plane projects to an ellipse: long horizontal
        // passes, foreshortened vertical passes below the face and above floor.
        float sx=119*(float)Math.Cos(angle),sy=77*(float)Math.Sin(angle),half=(float)Math.Sqrt(sx*sx+sy*sy),dx=sx/half,dy=sy/half;
        var saved=g.Save();g.TranslateTransform(center.X,center.Y);g.RotateTransform((float)(Math.Atan2(sy,sx)*180/Math.PI)+90);
        g.DrawImage(customStaff,new RectangleF(-8,-half,16,half*2));g.Restore(saved);
        float support=(float)CompanionMotion.Smooth((Math.Cos(angle)-.55)/.45);
        var rest=new PointF(153,308);var contact=new PointF(center.X-70*dx,center.Y-70*dy);
        DrawCustomTargetArm(g,rig,id,0,new PointF(rest.X+(contact.X-rest.X)*support,rest.Y+(contact.Y-rest.Y)*support));
        DrawCustomTargetArm(g,rig,id,1,center);
    }
    private static void DrawCustomArms(Graphics g,Rig rig,string id,string state,double phase,int onlyArm=-1)
    {
        float envelope=CompanionMotion.GestureEnvelope(phase);double t=phase*Math.PI*2;
        for(int arm=0;arm<2;arm++)
        {
            if(onlyArm>=0&&onlyArm!=arm)continue;
            PointF root=rig.Profile?CalibratedWalkingShoulder(id,arm):customNormalShoulders[arm];
            float reach=100*PreviewArmScale(id);
            PointF home=new PointF(root.X,root.Y+reach-.01f),target=home;
            if(rig.Profile)
            {
                // Let the whole arm hang from the shoulder, with only a small
                // opposing pendulum swing. Do not hold the forearm bent up.
                target=CompanionMotion.RelaxedWalkingPalm(root,phase,arm==0);
                target=new PointF(root.X+(target.X-root.X)*PreviewArmScale(id),root.Y+(target.Y-root.Y)*PreviewArmScale(id));
            }
            else if(arm==1)
            {
                PointF mouth=AttachedMouth(id,state,phase),goal=home;
                if(state=="review")goal=new PointF(mouth.X-6+(float)Math.Sin(t)*2,mouth.Y+10);
                if(state=="wiping-mouth")goal=new PointF(mouth.X-7+(float)Math.Sin(t*3)*9,mouth.Y+3);
                if(state=="eating")goal=new PointF(mouth.X-5,mouth.Y+5);
                if(state=="waving")goal=new PointF(root.X+28+(float)Math.Sin(t*2)*4,mouth.Y+(customCharacterId=="sun-wukong"?4:-10));
                if(state=="punching")goal=new PointF(root.X+41+(float)Math.Sin(t)*4,root.Y+15);
                if(state=="waving")
                {DrawCustomTargetArm(g,rig,id,arm,home,goal,envelope);continue;}

                target=new PointF(home.X+(goal.X-home.X)*envelope,home.Y+(goal.Y-home.Y)*envelope);
                // Raise on the outside of the body, avoiding a folded hand
                // target passing through the shoulder's singular position.
                if(state=="review"||state=="wiping-mouth"||state=="eating"||state=="waving")target.X+=80*envelope*(1-envelope);
            }
            DrawCustomTargetArm(g,rig,id,arm,target);
        }
    }
    private static void DrawCustomTargetArm(Graphics g,Rig rig,string id,int arm,PointF grip,PointF? waveGoal=null,float waveBlend=0)
    {
        Bitmap image=arm==1&&customState=="waving"&&customWaveArm!=null?customWaveArm:rig.Parts[arm+2];CustomArmArt art;
        if(!CustomArms.TryGetValue(image,out art))CustomArms.Add(image,art=new CustomArmArt(image,rig.ArmAttachments[arm],rig.ArmGrips[arm],100*PreviewArmScale(id)));
        PointF root=rig.Profile?CalibratedWalkingShoulder(id,arm):customNormalShoulders[arm];
        var pose=waveGoal.HasValue?CompanionMotion.OutwardWavePose(root,grip,waveGoal.Value,waveBlend,art.UpperLength,art.LowerLength,1):CompanionMotion.SolveArm(root,grip,art.UpperLength,art.LowerLength,rig.Profile?-1:1);
        if(waveGoal.HasValue){grip=pose[2];RecordWavePose(customCharacterId,pose,waveBlend,art.UpperLength,art.LowerLength);}

        CustomArmAudit.Add(new{state=customState,frame=customFrame,arm=arm,profile=rig.Profile,bodyAlreadyDrawn=customTorsoDrawn,shoulder=pose[0],elbow=pose[1],palm=pose[2],requested=grip,upperLength=art.UpperLength,lowerLength=art.LowerLength,sourcePalm=art.Palm,sourceShoulder=art.Shoulder,sourceElbow=art.Elbow});
        DrawRigidArmPart(g,art.Upper,art.Shoulder,art.Elbow,pose[0],pose[1]);
        DrawRigidArmPart(g,art.Lower,art.Elbow,art.Palm,pose[1],pose[2]);
    }
    private static void DrawRigidArmPart(Graphics g,Bitmap image,PointF sourceStart,PointF sourceEnd,PointF targetStart,PointF targetEnd)
    {
        float sx=sourceEnd.X-sourceStart.X,sy=sourceEnd.Y-sourceStart.Y,tx=targetEnd.X-targetStart.X,ty=targetEnd.Y-targetStart.Y,den=sx*sx+sy*sy;
        float a=(tx*sx+ty*sy)/den,b=(ty*sx-tx*sy)/den;
        var saved=g.Save();
        using(var transform=new Matrix(a,b,-b,a,targetStart.X-a*sourceStart.X+b*sourceStart.Y,targetStart.Y-b*sourceStart.X-a*sourceStart.Y))g.MultiplyTransform(transform);
        g.DrawImage(image,new Rectangle(0,0,image.Width,image.Height),new Rectangle(0,0,image.Width,image.Height),GraphicsUnit.Pixel);g.Restore(saved);
    }
    private static void DrawCalibratedNativeArms(Graphics g,Rig rig,string id,string state,double phase)
    {
        DrawRelaxedNativeArm(g,rig,id,0);
        if(state=="idle"||state=="waiting"||state=="failed"||state=="jumping")
        {DrawRelaxedNativeArm(g,rig,id,1);return;}
        float scale=PreviewArmScale(id),blend=CompanionMotion.GestureEnvelope(phase);
        PointF root=CalibratedShoulder(id,1),home=new PointF(root.X-4*scale,root.Y+98*scale);
        PointF mouth=AttachedMouth(id,state,phase),goal=home;
        double t=phase*Math.PI*2;
        if(state=="waving")goal=new PointF(root.X+32+(float)Math.Sin(t*2)*4,mouth.Y-8);
        if(state=="wiping-mouth")goal=new PointF(mouth.X-4+(float)Math.Sin(t*3)*7,mouth.Y+3);
        if(state=="review")goal=new PointF(mouth.X-6+(float)Math.Sin(t)*2,mouth.Y+10);
        if(state=="eating")goal=new PointF(mouth.X-4,mouth.Y+5);
        if(state=="punching")goal=new PointF(root.X+44+(float)Math.Sin(t)*4,root.Y+15);
        if(state=="waving")
        {DrawNativeWavingArm(g,rig,id,home,goal,blend);return;}

        PointF target=new PointF(home.X+(goal.X-home.X)*blend+60*blend*(1-blend),home.Y+(goal.Y-home.Y)*blend);
        DrawNativeTargetArm(g,rig,id,1,target);
    }
    private static void DrawNativeWavingArm(Graphics g,Rig rig,string id,PointF home,PointF raised,float blend)
    {
        Bitmap image=rig.Parts[3];CustomArmArt art;
        if(!CustomArms.TryGetValue(image,out art))CustomArms.Add(image,art=new CustomArmArt(image,rig.ArmAttachments[1],rig.ArmGrips[1],100*PreviewArmScale(id)));
        var pose=CompanionMotion.OutwardWavePose(CalibratedShoulder(id,1),home,raised,blend,art.UpperLength,art.LowerLength,1);
        RecordWavePose(id,pose,blend,art.UpperLength,art.LowerLength);
        DrawRigidArmPart(g,art.Upper,art.Shoulder,art.Elbow,pose[0],pose[1]);
        DrawRigidArmPart(g,art.Lower,art.Elbow,art.Palm,pose[1],pose[2]);
    }
    private static void RecordWavePose(string id,PointF[] pose,float blend,float upper,float lower)
    {WaveArmAudit.Add(new{id=id,frame=customFrame,blend=blend,shoulder=pose[0],elbow=pose[1],palm=pose[2],upperLength=upper,lowerLength=lower});}
    private static PointF AttachedMouth(string id,string state,double phase)
    {
        PointF mouth=customTail?customMouth:CompanionMotion.GestureMouth(id);var view=Calibration(id).normal;
        // The expression landmarks inherit exactly the same calibrated head matrix.
        using(var bitmap=new Bitmap(1,1))using(var g=Graphics.FromImage(bitmap))
        {
            PointF root=Point(view.root),basePoint=new PointF(root.X,root.Y+view.lift);
            CompanionMotion.ApplyHeadAttachment(g,customCharacterId??id,state,phase);
            PointF[] points={basePoint,mouth};using(Matrix pose=g.Transform)pose.TransformPoints(points);
            return new PointF(points[1].X+view.collar[0]-points[0].X,points[1].Y+view.collar[1]-points[0].Y);
        }
    }
    private static void DrawNativeTargetArm(Graphics g,Rig rig,string id,int arm,PointF target)
    {
        Bitmap image=rig.Parts[arm+2];CustomArmArt art;
        if(!CustomArms.TryGetValue(image,out art))CustomArms.Add(image,art=new CustomArmArt(image,rig.ArmAttachments[arm],rig.ArmGrips[arm],100*PreviewArmScale(id)));
        PointF root=CalibratedShoulder(id,arm);
        float dx=target.X-root.X,dy=target.Y-root.Y,d=(float)Math.Sqrt(dx*dx+dy*dy),reach=art.UpperLength+art.LowerLength-.01f;
        if(d>reach)throw new InvalidDataException("Unreachable calibrated native gesture: "+id+" ("+d+" > "+reach+")");
        var pose=CompanionMotion.SolveArm(root,target,art.UpperLength,art.LowerLength,1);
        DrawRigidArmPart(g,art.Upper,art.Shoulder,art.Elbow,pose[0],pose[1]);
        DrawRigidArmPart(g,art.Lower,art.Elbow,art.Palm,pose[1],pose[2]);
    }
    private static void ExportAnatomyLayers(string id,string output)
    {
        if(!HasAnatomy(id))return;
        string directory=Path.Combine(output,"body-layers",customCharacterId??id);Directory.CreateDirectory(directory);
        foreach(bool profile in new[]{false,true})using(var body=new Bitmap(384,416,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(body))
        {
            Quality(g);var rig=profile?SideRigs[id]:Rigs[id];
            DrawProportionTorso(g,rig.Parts[1],profile?ProfileTorsoBox(rig,id):new RectangleF(86,191,212,177),profile,id);
            body.Save(Path.Combine(directory,profile?"profile.png":"normal.png"),ImageFormat.Png);
        }
    }
    private static void DrawRelaxedNativeArm(Graphics g,Rig rig,string id,int arm)
    {
        Bitmap image=rig.Parts[arm+2];CustomArmArt art;
        PointF palmUv=rig.ArmGrips[arm];
        // Frontal far-sleeve hems and ribbon tips contain warm embroidery.
        // Use reviewed skin palm centers, not the lowest warm-colored pixel.
        if(arm==0)palmUv=id=="orbit"?new PointF(.56f,.73f):id=="spark"?new PointF(.81f,.77f):new PointF(.49f,.84f);
        float scale=PreviewArmScale(id);
        if(!CustomArms.TryGetValue(image,out art))CustomArms.Add(image,art=new CustomArmArt(image,rig.ArmAttachments[arm],palmUv,100*scale));
        PointF root=CalibratedShoulder(id,arm),palm=CompanionMotion.RelaxedRestingPalm(root);
        if(HasAnatomy(id))
        {
            // Frontal torso center is x=192. Match shoulder heights and mirror
            // attachment distances, without mirroring the painted sleeves.
            root=CalibratedShoulder(id,arm);
            palm=new PointF(root.X+(arm==0?4:-4)*scale,root.Y+98*scale);
        }
        var pose=CompanionMotion.SolveArm(root,palm,art.UpperLength,art.LowerLength,arm==0?1:-1);
        DrawRigidArmPart(g,art.Upper,art.Shoulder,art.Elbow,pose[0],pose[1]);
        DrawRigidArmPart(g,art.Lower,art.Elbow,art.Palm,pose[1],pose[2]);
    }
    private sealed class CustomArmArt:IDisposable
    {
        internal readonly Bitmap Upper,Lower;
        internal readonly PointF Shoulder,Elbow,Palm;
        internal readonly float UpperLength,LowerLength;
        internal CustomArmArt(Bitmap source,PointF root,PointF palm,float reach=100)
        {
            Shoulder=new PointF(root.X*source.Width,root.Y*source.Height);Palm=new PointF(palm.X*source.Width,palm.Y*source.Height);
            int cut=(int)(source.Height*.44f);double weight=0,xsum=0;
            for(int y=cut-3;y<=cut+3;y++)for(int x=0;x<source.Width;x++){int alpha=source.GetPixel(x,y).A;if(alpha<128)continue;weight+=alpha;xsum+=(x+.5)*alpha;}
            if(weight==0)throw new InvalidDataException("Sleeve has no elbow coverage.");Elbow=new PointF((float)(xsum/weight),cut);
            float upper=Distance(Shoulder,Elbow),lower=Distance(Elbow,Palm),scale=reach/(upper+lower);UpperLength=upper*scale;LowerLength=lower*scale;
            Upper=MaskArm(source,cut,true);Lower=MaskArm(source,cut,false);
        }
        private static float Distance(PointF a,PointF b){return (float)Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));}
        private static Bitmap MaskArm(Bitmap source,int cut,bool upper)
        {
            Bitmap result=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
            // Overlap real painted sleeve texture at the elbow. The entire
            // forearm and palm share one similarity transform: no finger mesh.
            float overlap=source.Height*.055f;
            for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)
            {
                Color c=source.GetPixel(x,y);float coverage=upper?1-(float)CompanionMotion.Smooth((y-cut)/overlap):(float)CompanionMotion.Smooth((y-cut+overlap)/overlap);
                result.SetPixel(x,y,Color.FromArgb((int)(c.A*coverage),c.R,c.G,c.B));
            }
            return result;
        }
        public void Dispose(){Upper.Dispose();Lower.Dispose();}
    }
    private sealed class CustomArmLayout
    {
        public float[][] normalShoulders {get;set;}
        public CustomArmSpec[] normal {get;set;}
        public CustomArmSpec[] profile {get;set;}
    }
    private sealed class CustomArmSpec
    {
        public int height {get;set;}
        public bool flipX {get;set;}
    }
}

internal static class BuildCompanionRevision
{
    private static int Main(string[] args)
    {
        try{if(args.Length==4&&args[3]=="--attachment-samples"){BuildOriginalCharacters.AttachmentSamples=true;args=new[]{args[0],args[1],args[2]};}if(args.Length!=3)throw new ArgumentException("Usage: BuildCompanionRevision artwork-root character-profile.json new-output-pack");BuildOriginalCharacters.ExportCustom(args[0],args[1],args[2]);return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
