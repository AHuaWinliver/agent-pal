// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed partial class PetForm
    {
        private readonly ArtifactTravelClock artifactTravel=new ArtifactTravelClock();
        private Bitmap artifactCanvas;
        private Graphics artifactGraphics;
        private Image artifactFootSource;
        private float artifactFootRow;
        private Image packedFlightArtwork, localFlightArtwork;
        private bool packedArtworkIsWings, localArtworkIsWings, choosingFlightArtwork;
        private readonly ArtifactImageStore flightArtwork = new ArtifactImageStore(Program.StateDirectory);

        private void AdoptFlightArtwork(PetCharacter character, CharacterAnimationSet incoming)
        {
            DisposeFlightArtwork();
            packedFlightArtwork = incoming.Artifact;
            incoming.Artifact = null;
            packedArtworkIsWings = character.ArtifactResourceFile == CharacterPackCatalog.WingsFile;
            ReloadLocalFlightArtwork(character.Id);
        }

        private void ReloadLocalFlightArtwork(string id)
        {
            Image image = flightArtwork.Load(id, out localArtworkIsWings);
            if (localFlightArtwork != null) localFlightArtwork.Dispose();
            localFlightArtwork = image;
        }

        private void DisposeFlightArtwork()
        {
            if (packedFlightArtwork != null) { packedFlightArtwork.Dispose(); packedFlightArtwork = null; }
            if (localFlightArtwork != null) { localFlightArtwork.Dispose(); localFlightArtwork = null; }
        }

        private ToolStripMenuItem BuildFlightArtworkMenu()
        {
            var menu = new ToolStripMenuItem(UiLanguage.Text("飞行外观")) { Name = "flightArtwork" };
            var wings = new ToolStripMenuItem(UiLanguage.Text("选择玄翼图片…")) { Name = "selectFlightWings" };
            var platform = new ToolStripMenuItem(UiLanguage.Text("选择灵器图片…")) { Name = "selectFlightPlatform" };
            var reset = new ToolStripMenuItem(UiLanguage.Text("恢复角色飞行外观")) { Name = "resetFlightArtwork" };
            wings.Click += delegate { BeginInvoke((MethodInvoker)delegate { ChooseFlightArtwork(true); }); };
            platform.Click += delegate { BeginInvoke((MethodInvoker)delegate { ChooseFlightArtwork(false); }); };
            reset.Click += delegate { ResetFlightArtwork(); };
            menu.DropDownOpening += delegate
            {
                bool custom = localFlightArtwork != null || packedFlightArtwork != null;
                bool activeWings = localFlightArtwork != null ? localArtworkIsWings : packedArtworkIsWings;
                wings.Checked = custom && activeWings;
                platform.Checked = custom && !activeWings;
                reset.Enabled = !choosingFlightArtwork && flightArtwork.Contains(currentCharacterId);
            };
            menu.DropDownItems.AddRange(new ToolStripItem[] { wings, platform, reset });
            return menu;
        }

        private async void ChooseFlightArtwork(bool wings)
        {
            if (choosingFlightArtwork || closingPet || IsDisposed) return;
            choosingFlightArtwork = true;
            string id = currentCharacterId;
            try
            {
                using (var picker = new OpenFileDialog
                {
                    Title = UiLanguage.Text(wings ? "选择玄翼图片…" : "选择灵器图片…"),
                    Filter = UiLanguage.Text("图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"),
                    CheckFileExists = true, Multiselect = false, RestoreDirectory = true
                })
                {
                    if (picker.ShowDialog() != DialogResult.OK) return;
                    string path = picker.FileName;
                    await Task.Run(delegate { flightArtwork.Save(id, path, wings); });
                }
                if (!IsDisposed && !closingPet && id == currentCharacterId)
                {
                    ReloadLocalFlightArtwork(id);
                    RefreshPetVisual();
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !closingPet)
                    MessageBox.Show(ex.Message, UiLanguage.Text("飞行外观"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { choosingFlightArtwork = false; }
        }

        private void ResetFlightArtwork()
        {
            if (choosingFlightArtwork) return;
            try
            {
                flightArtwork.Reset(currentCharacterId);
                ReloadLocalFlightArtwork(currentCharacterId);
                RefreshPetVisual();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, UiLanguage.Text("飞行外观"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void PrepareArtifactTravel()
        {
            artifactTravel.Reset();artifactFootSource=null;
            if(reduceMotion||wanderPath.Count==0)return;
            Rectangle area=wanderWorkingArea.IsEmpty?System.Windows.Forms.Screen.FromRectangle(Bounds).WorkingArea:wanderWorkingArea;
            if(ArtifactTravelClock.RouteLength(Location,wanderPath)>=ArtifactTravelClock.Threshold(area))artifactTravel.Start();
        }
        private bool TryRenderArtifactTravel()
        {
            if(!artifactTravel.Active||reduceMotion)return false;
            List<Image> idle;if(!animations.TryGetValue("idle",out idle)||idle.Count==0)return false;
            Image rider=idle[0];
            if(!Object.ReferenceEquals(rider,artifactFootSource)){artifactFootSource=rider;artifactFootRow=ArtifactTravelArt.FootRow(rider);}
            if(artifactCanvas==null||artifactCanvas.Size!=ClientSize)
            {DisposeArtifactCanvas();artifactCanvas=new Bitmap(Math.Max(1,Width),Math.Max(1,Height),PixelFormat.Format32bppArgb);artifactGraphics=Graphics.FromImage(artifactCanvas);}
            PetCharacter character=PetCharacter.Find(currentCharacterId);
            ArtifactTravelArt.Render(artifactGraphics,ClientSize,rider,artifactFootRow,character.Id,character.ReminderStyle,artifactTravel.Envelope,artifactTravel.Seconds,wanderFacingLeft,
                localFlightArtwork ?? packedFlightArtwork, localFlightArtwork != null ? localArtworkIsWings : packedArtworkIsWings);
            AddInputHitHalo(artifactCanvas);PushLayeredBitmap(artifactCanvas);return true;
        }
        private void DisposeArtifactCanvas(){if(artifactGraphics!=null){artifactGraphics.Dispose();artifactGraphics=null;}if(artifactCanvas!=null){artifactCanvas.Dispose();artifactCanvas=null;}}
    }
}
