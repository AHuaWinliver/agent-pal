// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using AgentPal;
internal static class WukongImportTests
{
    [STAThread]private static int Main(string[] args)
    {
        try
        {
            if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")!="1")throw new Exception("Isolated test state required.");
            string root=Path.Combine(Program.StateDirectory,"Characters");
            using(var draft=CharacterPackDraft.FromZip(root,args[0],CancellationToken.None))
            {
                if(draft.Character.Id!="sun-wukong")throw new Exception("Unexpected companion ID.");
                using(Image cloud=draft.ReadArtifact())if(cloud==null||cloud.Width>512)throw new Exception("Cloud missing.");
                for(int phase=0;phase<24;phase++)
                using(Image right=draft.ReadPreviewFrame(false,phase))using(Image left=draft.ReadPreviewFrame(true,phase))
                    if(right.Width!=384||left.Height!=416)throw new Exception("Invalid moving frame.");
                PetCharacter character=draft.Install(false);CharacterAnimationSet animations;
                CharacterPackCatalog.ReadPack(character.ResourceRoot,true,out animations);
                using(animations)
                {
                    int count=0;foreach(var state in animations.Frames)count+=state.Value.Count;
                    if(count!=240||animations.Artifact==null)throw new Exception("Incomplete decoded animation set.");
                    foreach(var state in animations.Frames)foreach(Bitmap frame in state.Value)
                    {
                        if(frame.GetPixel(0,0).A>8||frame.GetPixel(383,415).A>8)throw new Exception("Opaque background or clipped frame: "+state.Key);
                    }
                }
            }
            using(var second=CharacterPackDraft.FromZip(root,args[0],CancellationToken.None))
                if(!second.ReplacesExisting)throw new Exception("Installed pack is not detected for replacement.");
            Console.WriteLine("PASS actual ZIP preview, 24 left/right phases, cloud, installation, all 240 decoded frames and replacement detection.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
