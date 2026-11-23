// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Collections.Generic;

internal static class WalkingArmLayersTests
{
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private static Type generator;
    private static object Call(string name,params object[] args)
    {foreach(var method in generator.GetMethods(Static))if(method.Name==name&&method.GetParameters().Length==args.Length)return method.Invoke(null,args);throw new MissingMethodException(name);}
    private static void Parent(Graphics g,bool mirror,double phase)
    {
        Call("Quality",g);if(mirror){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
        double t=phase*Math.PI*2;g.TranslateTransform((float)Math.Sin(t)*.9f,(float)Math.Cos(t*2)*.75f);
        g.TranslateTransform(192,260);g.RotateTransform((float)Math.Sin(t)*.45f);g.TranslateTransform(-192,-260);
    }
    private static int Main(string[] args)
    {
        try
        {
            generator=Assembly.LoadFrom(args[0]).GetType("BuildOriginalCharacters");
            var rigType=generator.GetNestedType("Rig",BindingFlags.NonPublic);
            var metadata=new JavaScriptSerializer().Deserialize<Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(args[1],"side-rigs","regions.json")));
            Call("LoadAnatomy",args[1]);
            var normalMetadata=new JavaScriptSerializer().Deserialize<Dictionary<string,int[][]>>(File.ReadAllText(Path.Combine(args[1],"rigs","regions.json")));
            var normalRigs=(System.Collections.IDictionary)generator.GetField("Rigs",Static).GetValue(null);
            int poses=0,covered=0;
            foreach(string id in new[]{"orbit","spark","moss"})
            {
                object rig=Activator.CreateInstance(rigType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{Path.Combine(args[1],"side-rigs",id+".png"),metadata[id]},null);
                object normal=Activator.CreateInstance(rigType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{Path.Combine(args[1],"rigs",id+".png"),normalMetadata[id]},null);
                normalRigs.Add(id,normal);
                try
                {
                    var parts=(Bitmap[])rigType.GetField("Parts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rig);
                    int memberCovered=0;
                    foreach(string state in new[]{"running-left","running-right"})for(int frame=0;frame<24;frame++)
                    using(var scene=new Bitmap(384,416))using(var far=new Bitmap(384,416))using(var body=new Bitmap(384,416))using(var reference=new Bitmap(384,416))
                    {
                        double phase=frame/24.0;bool mirror=state=="running-left";
                        using(var g=Graphics.FromImage(scene)){Call("Quality",g);if(mirror){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}Call("ProfileHuman",g,rig,state,phase,id);}
                        var box=(RectangleF)Call("ProfileTorsoBox",rig,id);
                        using(var g=Graphics.FromImage(far)){Parent(g,mirror,phase);Call("DrawWalkingBackgroundArm",g,rig,id,state,phase);}
                        using(var g=Graphics.FromImage(body)){Parent(g,mirror,phase);Call("DrawProportionTorso",g,parts[1],box,true,id);}
                        using(var g=Graphics.FromImage(reference))
                        {
                            Parent(g,mirror,phase);Call("DrawProportionTorso",g,parts[1],box,true,id);
                            Call("DrawProportionHeadLayer",g,rig,id,state,phase,0,false);
                            Call("DrawWalkingForegroundArms",g,rig,id,state,phase);
                        }
                        // Include the shoulder/collar overlap: relaxed straight
                        // arms need not cross the waist like the earlier pose.
                        // At all heights, an opaque torso must completely hide
                        // the far sleeve. Compare actual composed pixels against
                        // the torso/near-arm result, not source-code order text.
                        for(int y=210;y<350;y+=2)for(int x=65;x<330;x+=2)
                        {
                            if(far.GetPixel(x,y).A<220||body.GetPixel(x,y).A<254)continue;
                            Color expected=reference.GetPixel(x,y),actual=scene.GetPixel(x,y);memberCovered++;
                            if(actual.A<250||Math.Abs(expected.R-actual.R)>3||Math.Abs(expected.G-actual.G)>3||Math.Abs(expected.B-actual.B)>3)
                                throw new Exception("Far arm appears through torso: "+id+" "+state+" frame="+frame+" at "+x+","+y);
                        }
                        poses++;
                    }
                    if(memberCovered<100)throw new Exception("Insufficient overlapping sleeve/body pixels: "+id);covered+=memberCovered;
                }
                finally{normalRigs.Remove(id);((IDisposable)normal).Dispose();((IDisposable)rig).Dispose();}
            }
            Console.WriteLine("PASS "+poses+" actual walking composites in both directions, "+covered+" covered far-arm pixels; torso occludes far sleeve and near sleeve remains in front.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
