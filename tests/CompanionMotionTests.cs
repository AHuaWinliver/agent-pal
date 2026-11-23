// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using AgentPal;

internal static class CompanionMotionTests
{
    private static void Assert(bool ok,string message) { if(!ok)throw new Exception(message); }
    private static double Distance(PointF a,PointF b) { return Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)); }
    private static PointF TexturePoint(PointF[] path,PointF uv,float width) {
        double p=uv.Y*(path.Length-1);int i=Math.Min(path.Length-2,(int)p);float f=(float)(p-i),dx=path[i+1].X-path[i].X,dy=path[i+1].Y-path[i].Y,length=(float)Math.Sqrt(dx*dx+dy*dy),offset=(uv.X-.5f)*width;
        return new PointF(path[i].X+dx*f+dy/length*offset,path[i].Y+dy*f-dx/length*offset);
    }
    private static PointF[] Arm(double phase,bool left) {
        return CompanionMotion.ArmPath((float)CompanionMotion.RelaxedWalkingSwing(phase,left),CompanionMotion.ElbowFlex(phase,left,"running-right"),146,left,"running-right",phase,24);
    }
    private static void CheckNeck(string id)
    {
        float min=100,max=-100;PointF previous=new PointF(),first=new PointF();
        using(Bitmap bitmap=new Bitmap(1,1))using(Graphics g=Graphics.FromImage(bitmap))
            for(int f=0;f<=240;f++)
            {
                double phase=f/240.0;float angle=CompanionMotion.WalkingNeckAngle(id,phase);
                Assert(Math.Abs(angle)<2.7,"Excessive neck angle");min=Math.Min(min,angle);max=Math.Max(max,angle);
                g.ResetTransform();CompanionMotion.ApplyWalkingNeck(g,id,phase);
                PointF root=CompanionMotion.WalkingNeckAnchor(id);
                PointF target=id=="pippin"?CompanionMotion.TurtleSocket(phase,true):CompanionMotion.WalkingBodyPoint(root);
                PointF[] points=new[]{root,id=="pippin"?new PointF(100,215):new PointF(192,35)};
                using(Matrix matrix=g.Transform)matrix.TransformPoints(points);
                Assert(Distance(points[0],target)<.0002,"Neck separates from collar/shell socket: "+id+" frame "+f+" got "+points[0]+" expected "+target);
                if(f==0)first=points[1];else Assert(Distance(previous,points[1])<.5,"Neck motion discontinuity");
                if(f==240)Assert(Distance(first,points[1])<.001,"Neck loop seam");
                previous=points[1];
            }
        Assert(max-min>2.8,"Neck motion not visible");
        Console.WriteLine("PASS "+id+": continuous delayed neck movement, stable collar anchor and seamless cycle");
    }
    private static void CheckDirection(string id)
    {
        using(Bitmap bitmap=new Bitmap(1,1))using(Graphics g=Graphics.FromImage(bitmap))
            foreach(string state in new[]{"running-left","running-right"})for(int frame=0;frame<24;frame++)
            {
                double phase=frame/24.0;int sign=state=="running-right"?1:-1;
                g.ResetTransform();if(CompanionMotion.MirrorWalkingFace(id,state)){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
                CompanionMotion.ApplyWalkingNeck(g,id,phase);
                PointF root=CompanionMotion.WalkingNeckAnchor(id);PointF[] face={root,new PointF(root.X+(id=="pippin"?-20:20),root.Y)};
                using(Matrix matrix=g.Transform)matrix.TransformPoints(face);
                Assert((face[1].X-face[0].X)*sign>19,"Head points away from travel direction");
                if(id!="pippin") {
                    g.ResetTransform();if(state=="running-left"){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
                    CompanionMotion.ApplyWalkingBody(g);PointF[] body={root,new PointF(220,220),new PointF(170,220)};
                    using(Matrix matrix=g.Transform)matrix.TransformPoints(body);
                    Assert(Distance(body[0],face[0])<.0003,"Head root differs from actual projected garment socket");
                    Assert((body[1].X-body[2].X)*sign>39,"Body faces opposite to travel");
                }
                Rectangle boot=new Rectangle(0,0,200,180);PointF cuff=new PointF(60,12),sole=new PointF(192,396);
                PointF heel=CompanionMotion.WalkingBootPoint(boot,cuff,new PointF(0,180),sole,0),toe=CompanionMotion.WalkingBootPoint(boot,cuff,new PointF(200,180),sole,0);
                if(state=="running-left"){heel.X=384-heel.X;toe.X=384-toe.X;}
                Assert((toe.X-heel.X)*sign>39&&toe.Y==396&&heel.Y==396,"Boot toe/heel direction or contact plane wrong");
                PointF baseRoot=CompanionMotion.NeckAnchor(id);PointF[] anchor={baseRoot};g.ResetTransform();CompanionMotion.ApplyHeadAttachment(g,id,"review",phase);
                using(Matrix matrix=g.Transform)matrix.TransformPoints(anchor);
                Assert(Distance(anchor[0],id=="pippin"?CompanionMotion.TurtleSocket(phase,false):baseRoot)<.0002,"Interaction detaches neck");
                foreach(float scale in new[]{.25f,1f,2f}) {
                    g.ResetTransform();g.TranslateTransform(7,11);g.ScaleTransform(scale,scale);
                    PointF expectedRoot=id=="pippin"?CompanionMotion.TurtleSocket(phase,true):CompanionMotion.WalkingBodyPoint(root);
                    PointF[] expected={expectedRoot};using(Matrix before=g.Transform)before.TransformPoints(expected);
                    CompanionMotion.ApplyWalkingNeck(g,id,phase);PointF[] actual={root};using(Matrix after=g.Transform)after.TransformPoints(actual);
                    Assert(Distance(expected[0],actual[0])<.0003,"Scaled head separates from body socket");
                }
            }
        Console.WriteLine("PASS "+id+": both directions, attached neck roots and aligned head/body"+(id=="pippin"?"":" / boot toe / heel"));
    }
    private static int Main() {
        try {
            foreach(string id in new[]{"orbit","spark","moss","pippin"})foreach(string state in id=="pippin"?new[]{"waving","punching","wiping-mouth"}:new[]{"waving","wiping-mouth"}) {
                PointF rootUv=new PointF(.51f,.055f),grip=id=="orbit"?new PointF(.60f,.66f):id=="spark"?new PointF(.20f,.80f):id=="moss"?new PointF(.46f,.80f):new PointF(.47f,.935f);
                float width=id=="pippin"?52:64,height=id=="pippin"?74:126;PointF[] previous=null,first=null;
                double minX=1000,maxX=-1000;
                for(int f=0;f<=240;f++) {
                    double phase=f/240.0;PointF[] path=CompanionMotion.GesturePath(id,state,phase,rootUv,grip,width,height,48);
                    PointF actual=TexturePoint(path,grip,width),target=CompanionMotion.GestureGrip(id,state,phase);
                    Assert(Distance(actual,target)<.001,"Gesture does not track the real palm / claw landmark");
                    if(previous!=null)Assert(Distance(TexturePoint(previous,grip,width),actual)<4.5,"Gesture hand target jumps between phases");
                    PointF sourceRoot=TexturePoint(path,rootUv,width),root=id=="pippin"?CompanionMotion.TurtleLegSocket(2,0,false):CompanionMotion.ShoulderAnchor(id,false);
                    Assert(Distance(sourceRoot,root)<.001,"Gesture pulls the shoulder / shell socket loose");
                    if(f==0)first=path;
                    for(int i=0;i<path.Length;i++) {
                        // A loose flared cuff extends beyond the palm and can
                        // sweep faster than the hand itself (checked above).
                        if(previous!=null)Assert(Distance(previous[i],path[i])<10,"Gesture cloth jumps between phases: "+id+" "+state+" f="+f);
                        if(f==240)Assert(Distance(first[i],path[i])<.001,"Gesture return-to-rest loop seam");
                    }
                    if(state=="wiping-mouth"&&CompanionMotion.GestureEnvelope(phase)>=.99999f) {
                        Assert(Math.Abs(actual.Y-CompanionMotion.GestureMouth(id).Y)<=4.01,"Wipe misses lip height");minX=Math.Min(minX,actual.X);maxX=Math.Max(maxX,actual.X);
                    }
                    if(id!="pippin"&&CompanionMotion.GestureEnvelope(phase)>=.99999f)
                        Assert(path[48].X<path[40].X,"Raised right forearm must point from the outer elbow back toward the hand");
                    previous=path;
                }
                if(state=="wiping-mouth")Assert(maxX-minX>12,"Wipe has no visible horizontal stroke");
            }
            PointF[] failedFar=CompanionMotion.ArmPath(6,12,146,true,"failed-outward",0,24),failedNear=CompanionMotion.ArmPath(-6,12,146,false,"failed-outward",0,24);
            Assert(failedFar[24].X<-12&&failedNear[24].X>12,"Failed arms turn inward instead of hanging outwards");
            Console.WriteLine("PASS real palm / claw contact, fixed gesture sockets, visible wiping strokes, smooth raise / lower cycles and outward failed arms");
            Assert(CompanionMotion.GesturePlaybackInterval("orbit","waving",16)==40&&CompanionMotion.GesturePlaybackInterval("pippin","punching",16)==40&&CompanionMotion.GesturePlaybackInterval("moss","wiping-mouth",16)==40,"Full-range gestures run too quickly");
            Assert(CompanionMotion.GesturePlaybackInterval("orbit","running-right",28)==28&&CompanionMotion.GesturePlaybackInterval("orbit","punching",16)==16&&CompanionMotion.GesturePlaybackInterval("moss","review",16)==16,"Gesture timing changes unrelated actions");
            foreach(string id in new[]{"orbit","moss"}) {
                float far=CompanionMotion.WalkingFootCenter(id,0),near=CompanionMotion.WalkingFootCenter(id,1);
                Assert(near-far>=4&&near-far<=8,"Side-view legs retain a frontal hip spread");
                using(Bitmap canvas=new Bitmap(1,1))using(Graphics g=Graphics.FromImage(canvas))for(int f=0;f<=240;f++) {
                    double phase=f/240.0,t=phase*Math.PI*2;
                    g.ResetTransform();g.TranslateTransform((float)Math.Sin(t)*.9f,(float)Math.Cos(t*2)*.75f);g.TranslateTransform(192,260);g.RotateTransform((float)Math.Sin(t)*.45f);g.TranslateTransform(-192,-260);
                    for(int leg=0;leg<2;leg++) {
                        PointF[] parent={new PointF(CompanionMotion.WalkingFootCenter(id,leg),311)};
                        using(Matrix matrix=g.Transform)matrix.TransformPoints(parent);
                        Assert(Distance(parent[0],CompanionMotion.WalkingHipPoint(id,leg,phase))<.0003,"Hip is not attached to torso transform: "+id+" frame="+f+" expected="+parent[0]+" actual="+CompanionMotion.WalkingHipPoint(id,leg,phase));
                        Assert(Distance(CompanionMotion.WalkingHipPoint(id,leg,0),CompanionMotion.WalkingHipPoint(id,leg,1))<.0001,"Hip attachment loop seam");
                    }
                }
            }
            Console.WriteLine("PASS narrow side-view pelvis, torso-parented hip roots and seamless hip cycles for orbit / moss");
            foreach(bool profile in new[]{false,true}) {
                float start=profile?296:322,rootX=profile?151:144;
                for(int y=190;y<=start;y+=2)for(int x=50;x<=180;x+=2) {
                    PointF p=new PointF(x,y);Assert(Distance(p,CompanionMotion.TurtleNeckPoint(p,profile))<.00001,"Neck correction reshapes turtle face / skull");
                }
                PointF left=CompanionMotion.TurtleNeckPoint(new PointF(rootX-35,start+40),profile),right=CompanionMotion.TurtleNeckPoint(new PointF(rootX+35,start+40),profile);
                Assert(right.X-left.X<54&&right.X-left.X>49,"Turtle neck root is still too broad or over-pinched");
                Assert((left.Y+right.Y)/2-start<18,"Turtle neck still hangs below the shell as a broad bib");
                for(int x=80;x<=180;x+=5) {
                    PointF previous=CompanionMotion.TurtleNeckPoint(new PointF(x,start),profile);
                    for(int i=1;i<=180;i++) {
                        PointF p=CompanionMotion.TurtleNeckPoint(new PointF(x,start+i*.25f),profile);
                        Assert(p.Y>previous.Y&&Distance(p,previous)<.5,"Turtle neck folds or jumps at jaw transition");previous=p;
                    }
                }
            }
            Console.WriteLine("PASS turtle jaw remains unchanged, neck root tapers and retracts continuously without folded geometry");
            for(int f=0;f<=240;f++) {
                double phase=f/240.0;
                float lift=phase<.6?0:(float)(10*Math.Sin(Math.PI*(phase-.6)/.4));
                var bounds=new Rectangle(0,0,200,180);var cuff=new PointF(60,12);var sole=new PointF(192,396-lift);
                PointF pivot=CompanionMotion.WalkingBootPoint(bounds,cuff,new PointF(60,180),sole,phase);
                Assert(Distance(pivot,sole)<.0001,"Boot sole anchor drift");
                PointF heel=CompanionMotion.WalkingBootPoint(bounds,cuff,new PointF(0,180),sole,phase),toe=CompanionMotion.WalkingBootPoint(bounds,cuff,new PointF(200,180),sole,phase);
                Assert(Math.Abs(Distance(heel,toe)-40)<.0001,"Boot shape distorted across gait");
                Assert(heel.Y<=396.001&&toe.Y<=396.001,"Boot rolls through the ground");
                if(phase<.6)Assert(CompanionMotion.WalkingBootPitch(phase)==0,"Loaded boot not flat");
                foreach(bool gesture in new[]{false,true}) {
                    PointF[] path=CompanionMotion.TurtleLegPath(phase,true,gesture,56,24),neutral=CompanionMotion.TurtleLegPath(0,true,gesture,56,24);
                    for(int j=0;j<=3;j++)Assert(Distance(path[j],neutral[j])<.0001,"Turtle root moves with its foot");
                    if(f==240)Assert(Distance(path[24],neutral[24])<.0001,"Turtle gait seam");
                    foreach(float reach in new[]{-14f,0f,16f}) {
                        PointF[] extended=CompanionMotion.TurtleLegPathWithReach(phase,true,gesture,56,reach,24);
                        for(int j=0;j<=3;j++)Assert(Distance(extended[j],neutral[j])<.0001,"Turtle extension moves root cap");
                        for(int j=16;j<24;j++)Assert(Math.Abs(extended[j+1].X-extended[j].X)<.0001&&Math.Abs(extended[j+1].Y-extended[j].Y-56/24f)<.0001,"Turtle claws stretch or rotate independently");
                    }
                }
                for(int part=2;part<=5;part++) {
                    PointF root=CompanionMotion.TurtleLegSocket(part,phase,true),atRest=CompanionMotion.TurtleLegSocket(part,0,false);
                    Assert(root.X==atRest.X&&Math.Abs(root.Y-atRest.Y)<=.601,"Turtle socket moves away from body");
                }
            }
            Console.WriteLine("PASS complete rigid boots, exact sole anchors, flat stance / smooth swing, turtle pinned proximal limbs and cyclic lower-limb gait");
            foreach(bool left in new[]{true,false}) {
                PointF[] previous=null;
                for(int f=0;f<=240;f++) {
                    double phase=f/240.0;PointF[] path=Arm(phase,left);
                    double length=0;for(int j=1;j<path.Length;j++)length+=Distance(path[j-1],path[j]);
                    Assert(Math.Abs(length-146)<.001,"Sleeve length changed");
                    Assert(CompanionMotion.ElbowFlex(phase,left,"running-right")>=1 && CompanionMotion.ElbowFlex(phase,left,"running-right")<=3,"Walking elbow is held bent rather than relaxed");
                    // The sewn shoulder cap is intentionally fixed; compare the
                    // moving upper arm below it against the forearm instead.
                    PointF a=new PointF(path[8].X-path[7].X,path[8].Y-path[7].Y),b=new PointF(path[23].X-path[18].X,path[23].Y-path[18].Y);
                    double delta=Math.Abs(Math.Atan2(a.X,a.Y)-Math.Atan2(b.X,b.Y))*180/Math.PI;
                    Assert(delta<8,"Walking upper arm and forearm do not hang naturally straight");
                    Assert(path[24].Y-path[0].Y>140&&Math.Abs(path[24].X-path[0].X)<24,"Whole walking arm is not hanging below its shoulder");
                    if(previous!=null)Assert(Distance(path[24],previous[24])<3,"Hand discontinuity");
                    if(previous!=null)for(int j=0;j<=3;j++)Assert(Distance(path[j],previous[j])<.0001,"Sewn shoulder cap moved");
                    previous=path;
                }
                Assert(Distance(Arm(0,left)[24],Arm(1,left)[24])<.001,"Arm cycle seam");
            }
            Assert(Arm(0,true)[24].X<0 && Arm(0,false)[24].X>0,"Left front foot needs right front hand");
            Assert(Arm(.5,true)[24].X>0 && Arm(.5,false)[24].X<0,"Right front foot needs left front hand");
            foreach(string id in new[]{"orbit","spark","moss"}) {
                PointF l=CompanionMotion.ShoulderAnchor(id,true),r=CompanionMotion.ShoulderAnchor(id,false);
                Assert(l.X>=130&&l.X<=155&&r.X>=215&&r.X<=240&&l.Y>=210&&r.Y>=210,"Shoulder anchor outside reviewed garment sockets");
                PointF far=CompanionMotion.WalkingShoulderAnchor(id,true),near=CompanionMotion.WalkingShoulderAnchor(id,false);
                Assert(near.X-far.X>=4&&near.X-far.X<=10&&Math.Abs(near.Y-far.Y)<=3&&near.Y>=215&&near.Y<=236,"Profile shoulders must overlap in depth and match the redrawn side garment");
                foreach(string pose in new[]{"waving","review","eating","wiping-mouth"}) {
                    PointF[] a=CompanionMotion.ArmPath(-120,CompanionMotion.ElbowFlex(0,false,pose),108,false,pose,0,24);
                    PointF[] b=CompanionMotion.ArmPath(120,CompanionMotion.ElbowFlex(.5,false,pose),108,false,pose,.5,24);
                    for(int j=0;j<=3;j++)Assert(Distance(a[j],b[j])<.0001,"Gesture detaches shoulder cap");
                }
            }
            Console.WriteLine("PASS reviewed per-character shoulder anchors and phase-independent sewn sleeve roots");
            Console.WriteLine("PASS relaxed straight hanging arms, small opposite hand/foot swing, constant sleeve length and continuous cyclic joints");
            foreach(string id in new[]{"orbit","spark","moss","pippin"}) {
                CheckNeck(id);
                CheckDirection(id);
                WalkingExpressionClock clock=new WalkingExpressionClock(12345,id);
                int lastB=0,lastS=0,blinks=0,smiles=0;bool fullBlink=false,fullSmile=false;
                List<double> starts=new List<double>();
                for(int tick=0;tick<7200;tick++) {
                    int b,s;clock.Sample(tick/120.0,out b,out s);
                    Assert(b>=0&&b<=3&&s>=0&&s<=3,"Expression level bounds");
                    Assert(Math.Abs(b-lastB)<=1&&Math.Abs(s-lastS)<=1,"Abrupt expression transition");
                    if(b>0&&lastB==0){blinks++;starts.Add(tick/120.0);}
                    if(s>0&&lastS==0)smiles++;
                    fullBlink|=b==3;fullSmile|=s==3;lastB=b;lastS=s;
                }
                Assert(blinks>=7&&blinks<=20&&smiles>=4&&smiles<=10&&fullBlink&&fullSmile,"Expression cadence: "+id);
                bool varied=false;for(int j=2;j<starts.Count;j++)if(Math.Abs((starts[j]-starts[j-1])-(starts[1]-starts[0]))>.15)varied=true;
                Assert(varied,"Expressions must not repeat on a fixed gait beat");
                Console.WriteLine("PASS "+id+": "+blinks+" blinks / "+smiles+" subtle mouth changes per minute, smooth and independent of gait");
            }
            int restDuration=0;for(int i=0;i<8;i++)restDuration+=CompanionMotion.GesturePlaybackInterval("bailong","idle",40,8,i);
            Assert(restDuration>=3000&&CompanionMotion.GesturePlaybackInterval("bailong","idle",40,8,6)<=150,"Idle breathing or blink duration is unnatural");
            Assert(Math.Abs(CompanionMotion.GesturePlaybackInterval("sun-wukong","review",16,24)*24-1400)<24,"24-pose thinking cycle must retain its intended duration");
            Console.WriteLine("PASS readable idle breathing and blink dwell, and frame-count-aware thinking duration");
            foreach(int bend in new[]{-1,1})foreach(float radius in new[]{0f,8f,42f,80f,103f,150f})for(int f=0;f<=360;f++) {
                double t=f*Math.PI/180;var root=new PointF(224,217);var goal=new PointF(root.X+radius*(float)Math.Cos(t),root.Y+radius*(float)Math.Sin(t));
                var pose=CompanionMotion.SolveArm(root,goal,47,56,bend);
                Assert(Distance(pose[0],root)<.0001,"Imported shoulder leaves garment socket");
                Assert(Math.Abs(Distance(pose[0],pose[1])-47)<.0001&&Math.Abs(Distance(pose[1],pose[2])-56)<.0001,"Imported arm stretches rather than bends");
                Assert(!float.IsNaN(pose[1].X)&&!float.IsNaN(pose[2].Y),"Folded or unreachable arm produces invalid pose");
                if(radius>=10&&radius<=102)Assert(Distance(pose[2],goal)<.0001,"Reachable palm fails to meet its target");
            }
            Console.WriteLine("PASS fixed-length articulated upper arms and forearms, reachable palm contact, stable sockets and bounded unreachable targets");
            foreach(PointF root in new[]{new PointF(144,218),new PointF(224,217),new PointF(142,233),new PointF(258,233)}) {
                PointF hand=CompanionMotion.RelaxedRestingPalm(root);
                Assert(Math.Abs(hand.X-root.X)<.0001&&hand.Y-root.Y>99.9,"Resting hands must hang directly below the shoulders");
                for(int frame=0;frame<24;frame++)foreach(bool far in new[]{true,false}) {
                    PointF walking=CompanionMotion.RelaxedWalkingPalm(root,frame/24.0,far);
                    Assert(walking.Y-root.Y>99&&Math.Abs(walking.X-root.X)<11,"Walking hand is lifted or swings too widely");
                }
            }
            Console.WriteLine("PASS complete frontal resting arms hang vertically; walking arms remain low with a small pendulum swing");
            foreach(int direction in new[]{-1,1})foreach(float upper in new[]{39f,44.7f,50f})
            {
                float lower=88-upper;PointF root=new PointF(224,217),home=new PointF(224-direction*3.52f,303.24f),raised=new PointF(224+direction*28,156);
                PointF[] first=null,previous=null;
                for(int f=0;f<=240;f++)
                {
                    float blend=CompanionMotion.GestureEnvelope(f/240.0);
                    var pose=CompanionMotion.OutwardWavePose(root,home,raised,blend,upper,lower,direction);
                    Assert(direction*(pose[1].X-root.X)>=-.001,"Wave lowering folds its elbow into the torso");
                    Assert(Math.Abs(Distance(pose[0],pose[1])-upper)<.001&&Math.Abs(Distance(pose[1],pose[2])-lower)<.001,"Wave changes bone lengths");
                    if(f==0)first=pose;
                    if(previous!=null)Assert(Distance(pose[1],previous[1])<3&&Distance(pose[2],previous[2])<5,"Wave lowering flips its elbow or palm between frames");
                    if(blend==1)Assert(Distance(pose[2],raised)<.001,"Wave no longer reaches its raised palm target");
                    if(f==240)Assert(Distance(pose[1],first[1])<.001&&Distance(pose[2],first[2])<.001,"Wave does not return to the same resting joints");
                    previous=pose;
                }
            }
            Console.WriteLine("PASS complete wave raise/lower arcs keep elbows outside the torso, fixed bone lengths, continuous wrists and matching resting endpoints");
            return 0;
        } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
