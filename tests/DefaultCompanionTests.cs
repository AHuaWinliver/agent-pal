// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AgentPal;
internal static class DefaultCompanionTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static object Field(object value,string name){return value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    private static bool HasMenu(ToolStripItemCollection items,string id){foreach(ToolStripItem item in items){if(Convert.ToString(item.Tag)==id)return true;var child=item as ToolStripMenuItem;if(child!=null&&HasMenu(child.DropDownItems,id))return true;}return false;}
    private static string Order(PetCharacter[] profiles){return String.Join(",",Array.ConvertAll(profiles,p=>p.Id));}
    private static bool HasOrderedMenu(ToolStripItemCollection items,string expected)
    {
        var ids=new List<string>();foreach(ToolStripItem item in items)
        {
            string id=Convert.ToString(item.Tag);if(Array.IndexOf(PetCharacter.DefaultDisplayIds,id)>=0)ids.Add(id);
            var child=item as ToolStripMenuItem;if(child!=null&&HasOrderedMenu(child.DropDownItems,expected))return true;
        }
        return String.Join(",",ids.ToArray())==expected;
    }
    private static void CheckWalkingExpressionAttachments()
    {
        int poses=0;
        foreach(string id in new[]{"orbit","spark","moss"})
        {
            string folder=id=="orbit"?PetCharacter.AssetsRoot:Path.Combine(PetCharacter.AssetsRoot,"characters",id);
            var offsets=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<float[]>(File.ReadAllText(Path.Combine(folder,"profile-expressions","attachment.json")));
            var root=CompanionMotion.WalkingNeckAnchor(id);var source=new PointF(root.X-offsets[0],root.Y-offsets[1]);
            foreach(string state in new[]{"running-left","running-right"})for(int frame=0;frame<24;frame++)
            using(var bitmap=new Bitmap(1,1))using(var g=Graphics.FromImage(bitmap))
            {
                double t=frame/24.0*Math.PI*2;
                if(state=="running-left"){g.TranslateTransform(384,0);g.ScaleTransform(-1,1);}
                g.TranslateTransform((float)Math.Sin(t)*.9f,(float)Math.Cos(t*2)*.75f);
                g.TranslateTransform(192,260);g.RotateTransform((float)Math.Sin(t)*.45f);g.TranslateTransform(-192,-260);
                PointF[] expected={new PointF(root.X+offsets[2],root.Y+offsets[3])};using(Matrix matrix=g.Transform)matrix.TransformPoints(expected);
                g.ResetTransform();PetForm.ApplyWalkingExpressionTransform(g,id,state,frame,offsets);
                PointF[] actual={source};using(Matrix matrix=g.Transform)matrix.TransformPoints(actual);
                if(Math.Abs(actual[0].X-expected[0].X)>.001||Math.Abs(actual[0].Y-expected[0].Y)>.001)throw new Exception("Walking expression no longer follows the calibrated neck: "+id);
                poses++;
            }
        }
        Check(poses==144,"144 walking-expression transforms match the calibrated head/collar in both directions");
    }
    [STAThread]private static int Main(string[] args)
    {
        try
        {
            if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")!="1")throw new Exception("Isolated test required.");
            Environment.SetEnvironmentVariable("AGENT_PAL_TEST_ASSETS",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"));
            string user=PetForm.CharacterPackDirectory;
            Check(!Directory.Exists(user),"fresh-user fixture has no imported character directory");
            Check(PetCharacter.Defaults.Length==6&&PetCharacter.Available.Length==6,"six companions are available before importing any ZIP");
            const string expected="orbit,spark,moss,sun-wukong,bailong,pippin";
            Check(Order(PetCharacter.Defaults)==expected&&Order(PetCharacter.Available)==expected,"all default selectors keep Xiaomo last among built-ins");
            Check(Order(PetCharacter.All)=="orbit,spark,moss,pippin","fixed-rig generation and legacy role indices stay stable");
            var wukong=PetCharacter.Find("sun-wukong");
            Check(CompanionMotion.GesturePlaybackInterval("sun-wukong","review",16)==175&&CompanionMotion.GesturePlaybackInterval("sun-wukong","punching",16)==16,"chin-rub timing slows to a 1.4-second eight-pose cycle without changing staff timing");
            Check(wukong.Id=="sun-wukong"&&wukong.Name=="行者悟空"&&wukong.IsBundled&&!wukong.IsExternal&&wukong.UsesPackFrames&&wukong.ResourceRoot.StartsWith(PetCharacter.AssetsRoot,StringComparison.OrdinalIgnoreCase),"Wayfarer Wukong is built in and reuses the full-frame renderer");
            CharacterAnimationSet assets;CharacterPackCatalog.ReadPack(wukong.ResourceRoot,true,out assets);
            using(assets)
            {
                int frames=0;foreach(var state in assets.Frames){frames+=state.Value.Count;foreach(Bitmap frame in state.Value)CheckCorners(frame);}
                Check(frames==240&&assets.Frames["running-left"].Count==24&&assets.Frames["running-right"].Count==24,"all 240 transparent frames decode, including 24 phases per walking direction");
                Check(assets.Artifact!=null&&assets.Artifact.Width<=512,"flying cloud is included and decoded");
            }
            Check(PetForm.ValidateAssets(AppDomain.CurrentDomain.BaseDirectory),"application self-test validates the bundled default pack");
            CheckWalkingExpressionAttachments();
            var userOverride=new PetCharacter{Id="sun-wukong",Name="User Wukong",ResourceRoot=Path.Combine(Program.StateDirectory,"user-wukong")};
            PetCharacter.SetExternalProfiles(new[]{userOverride});
            Check(PetCharacter.Available.Length==6&&Object.ReferenceEquals(PetCharacter.Find("sun-wukong"),userOverride),"an existing user-imported Wukong wins without duplicate menu entries");
            Check(Order(PetCharacter.Available)=="orbit,spark,moss,bailong,pippin,sun-wukong","a user override follows all built-in companions");
            var extra=new PetCharacter{Id="extra-companion",Name="Extra",ResourceRoot=Path.Combine(Program.StateDirectory,"extra")};
            var turtle=new PetCharacter{Id="pippin",Name="Custom Xiaomo",ResourceRoot=Path.Combine(Program.StateDirectory,"turtle")};
            PetCharacter.SetExternalProfiles(new[]{extra,turtle,userOverride});
            Check(Order(PetCharacter.Available)=="orbit,spark,moss,bailong,extra-companion,pippin,sun-wukong"&&Object.ReferenceEquals(PetCharacter.Find("pippin"),turtle),"imports and overrides follow built-ins in stable ID order without duplicates");
            PetCharacter.SetExternalProfiles(null);
            Check(PetCharacter.Find("sun-wukong").Name=="行者悟空","removing an override reveals the default again");
            using(var form=new PetForm())
            {
                Check(HasMenu(form.ContextMenuStrip.Items,"sun-wukong"),"Wukong appears in the normal companion menu");
                Check(HasOrderedMenu(form.ContextMenuStrip.Items,expected),"actual switch-companion menu uses the shared default order");
                object tray=Field(form,"tray");object icon=tray.GetType().GetProperty("Icon").GetValue(tray,null);
                var select=typeof(PetForm).GetMethod("SetCharacter",BindingFlags.NonPublic|BindingFlags.Instance);
                foreach(string id in new[]{"sun-wukong","orbit","sun-wukong"})
                {select.Invoke(form,new object[]{id,false});Check((string)Field(form,"currentCharacterId")==id,"switch succeeds: "+id);}
                Check(Object.ReferenceEquals(icon,tray.GetType().GetProperty("Icon").GetValue(tray,null)),"the fixed AgentPal tray icon survives Wukong switching");
            }
            Check(Directory.GetDirectories(user).Length==0,"default loading does not copy over or populate the user's packs");
            Console.WriteLine("PASS default Wukong integration without model calls.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{PetCharacter.SetExternalProfiles(null);}
    }
    private static void CheckCorners(Bitmap frame){if(frame.Width!=384||frame.Height!=416||frame.GetPixel(0,0).A>8||frame.GetPixel(383,415).A>8)throw new Exception("Invalid/clipped default animation frame.");}
}
