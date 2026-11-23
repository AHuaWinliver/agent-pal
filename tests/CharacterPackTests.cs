// SPDX-License-Identifier: Apache-2.0
// Synthetic rectangles only: these fixtures test loading, not finished character artwork.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using AgentPal;

internal static class CharacterPackTests
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
    private static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args); }
    private static bool Until(Func<bool> condition, int milliseconds)
    { Stopwatch watch = Stopwatch.StartNew(); while (!condition() && watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(20); } return condition(); }
    private static string Hash(byte[] bytes) { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
    private static CharacterPackManifest Manifest(string folder) { return Json.Deserialize<CharacterPackManifest>(File.ReadAllText(Path.Combine(folder, "character.json"))); }
    private static void SaveManifest(string folder, CharacterPackManifest manifest) { File.WriteAllText(Path.Combine(folder, "character.json"), Json.Serialize(manifest), new UTF8Encoding(false)); }

    private static string WritePack(string parent, string id, string version, Color color, string displayName = null)
    {
        string folder = Path.Combine(parent, id); Directory.CreateDirectory(folder);
        var m = new CharacterPackManifest { schemaVersion = 1, id = id, version = version, name = displayName ?? "测试伙伴 " + version, persona = "原创的测试伙伴，" + version,
            quotes = new[] { "一起开始吧。", "先做一小步。", "歇歇眼睛吧。", "慢慢理清思路。", "我们稳稳地来。" }, reminderStyle = "beam", canvasWidth = 384, canvasHeight = 416, mouthX = .5f, mouthY = .4f,
            files = new Dictionary<string, string>() };
        foreach (string state in CharacterPackCatalog.States)
        {
            Directory.CreateDirectory(Path.Combine(folder, state));
            for (int f = 0; f < CharacterPackCatalog.FrameCount(state); f++)
            {
                string relative = state + "/" + f.ToString("00") + ".png";
                using (Bitmap image = new Bitmap(384, 416, PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(image))
                using (Brush fill = new SolidBrush(color))
                {
                    g.Clear(Color.Transparent); g.FillRectangle(fill, 160, 160, 60, 90); g.FillEllipse(fill, 90 + f, 80 + f, 18, 18);
                    image.Save(Path.Combine(folder, relative), ImageFormat.Png);
                }
                m.files.Add(relative, Hash(File.ReadAllBytes(Path.Combine(folder, relative))));
            }
        }
        SaveManifest(folder, m); return folder;
    }

    private static void MustReject(string folder, string reason)
    {
        CharacterAnimationSet assets = null; bool rejected = false;
        try { CharacterPackCatalog.ReadPack(folder, false, out assets); }
        catch { rejected = true; }
        finally { if (assets != null) assets.Dispose(); }
        Require(rejected, reason);
    }

    private static void TestValidation(string root)
    {
        var catalog = new CharacterPackCatalog(root);
        string folder = WritePack(root, "test-walker", "0.1.0", Color.ForestGreen);
        string frame = Path.Combine(folder, "idle", "00.png"); byte[] original = File.ReadAllBytes(frame);
        string revision;
        using (CharacterPackScan scan = catalog.Scan(null, null))
        {
            Require(scan.Errors.Length == 0 && scan.Characters.Length == 1, "new data-only pack discovered with 240 verified PNG frames");
            revision = scan.Characters[0].ResourceRevision;
            PetCharacter.SetExternalProfiles(scan.Characters);
            Require(PetCharacter.Available.Length == PetCharacter.Defaults.Length+1 && PetCharacter.All.Length == 4 && PetCharacter.Find("test-walker").IsExternal && PetCharacter.ValidateProfiles(), "dynamic profiles do not change built-in generation or legacy aliases");
        }
        CharacterAnimationSet loaded;
        CharacterPackCatalog.ReadPack(folder, true, out loaded);
        using (loaded) using (FileStream exclusive = File.Open(frame, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Require(loaded.Frames["running-left"].Count == 24, "decoded frames do not lock source files");

        File.WriteAllBytes(frame, new byte[] { 1, 2, 3 });
        using (CharacterPackScan scan = catalog.Scan("test-walker", revision))
            Require(scan.Errors.Length == 1 && scan.Characters[0].ResourceRevision == revision && scan.PreparedAssets == null, "partial/corrupt update keeps last known profile without replacing active frames");
        File.WriteAllBytes(frame, original);
        CharacterPackManifest m = Manifest(folder); m.id = "../outside"; SaveManifest(folder, m); MustReject(folder, "path traversal id rejected before image loading");
        m.id = "orbit"; SaveManifest(folder, m); MustReject(folder, "built-in ID collision rejected");
        m.id = "test-walker"; m.schemaVersion = 999; SaveManifest(folder, m); MustReject(folder, "unknown schema rejected");
        m.schemaVersion = 1; m.reminderStyle = "execute-code"; SaveManifest(folder, m); MustReject(folder, "executable/custom code styles rejected");
        m.reminderStyle = "beam"; SaveManifest(folder, m);
        string executable = Path.Combine(folder, "plugin.dll"); File.WriteAllBytes(executable, new byte[] { 1 }); MustReject(folder, "executable resource files rejected"); File.Delete(executable);
        File.Delete(frame); MustReject(folder, "missing animation frame rejected"); File.WriteAllBytes(frame, original);
        using (Bitmap wrong = new Bitmap(16, 16)) wrong.Save(frame, ImageFormat.Png);
        m.files["idle/00.png"] = Hash(File.ReadAllBytes(frame)); SaveManifest(folder, m); MustReject(folder, "wrong PNG dimensions rejected even with a matching hash");
        File.WriteAllBytes(frame, original); m.files["idle/00.png"] = Hash(original);
        string oldHash = m.files["running-right/01.png"]; m.files["running-right/01.png"] = m.files["running-right/00.png"];
        SaveManifest(folder, m); MustReject(folder, "duplicated walking phases cannot be used to pad 24 frames");
        m.files["running-right/01.png"] = oldHash; SaveManifest(folder, m);
        byte[] inflatedHeader = (byte[])original.Clone(); inflatedHeader[16] = 0x7f; File.WriteAllBytes(frame, inflatedHeader);
        m.files["idle/00.png"] = Hash(inflatedHeader); SaveManifest(folder, m); MustReject(folder, "oversized PNG header rejected before decompression");
        File.WriteAllBytes(frame, original); m.files["idle/00.png"] = Hash(original); SaveManifest(folder, m);
        byte[] json = File.ReadAllBytes(Path.Combine(folder, "character.json")); File.WriteAllText(Path.Combine(folder, "character.json"), "{");
        using (CharacterPackScan scan = catalog.Scan("test-walker", revision)) Require(scan.Errors.Length > 0 && scan.Characters[0].ResourceRevision == revision, "broken JSON does not remove the last known character");
        File.WriteAllBytes(Path.Combine(folder, "character.json"), json);
        WritePack(root, "test-walker", "0.2.0", Color.CornflowerBlue);
        using (CharacterPackScan scan = catalog.Scan("test-walker", revision))
            Require(scan.PreparedAssets != null && scan.Characters[0].ResourceRevision != revision, "selected update is decoded off-thread before UI delivery");
        Directory.Move(folder, Path.Combine(root, ".removed-test-walker"));
        using (CharacterPackScan scan = catalog.Scan(null, null)) Require(scan.Characters.Length == 0, "removed packs disappear and dot-prefixed staging folders are ignored");
        PetCharacter.SetExternalProfiles(null);
    }

    private static bool CurrentPixel(PetForm pet, Color color)
    {
        var frames = (Dictionary<string, List<Image>>)Field(pet, "animations");
        return ((Bitmap)frames["idle"][0]).GetPixel(180, 200).ToArgb() == color.ToArgb();
    }

    private static void TestLive(string stateRoot)
    {
        string packs = Path.Combine(stateRoot, "characters"); Directory.CreateDirectory(packs);
        string folder = WritePack(packs, "test-walker", "0.1.0", Color.ForestGreen);
        using (PetForm pet = new PetForm())
        {
            pet.Show(); ((System.Windows.Forms.Timer)Field(pet, "timer")).Stop();
            Require(PetCharacter.Find("test-walker").IsExternal, "installed app discovers pack on startup without recompilation");
            Call(pet, "SetCharacter", "test-walker", false);
            Require((string)Field(pet, "currentCharacterId") == "test-walker" && CurrentPixel(pet, Color.ForestGreen) && pet.LayeredRenderSucceeded, "external character switches through the existing renderer");
            Require(Field(pet, "walkingExpressionClock") == null, "external full-frame faces do not receive another character's head/neck overlays");
            Require(pet.ValidateLayeredAnimationAdvance(), "external 24-phase walking advances through lazy native frame cache");
            Require(PetBrain.BuildConversationPrompt(PetCharacter.Find("test-walker"), "你好").Contains("原创的测试伙伴"), "external persona drives the existing AI conversation prompt");
            Require(ReminderPerformance.Supports(PetCharacter.Find("test-walker").ReminderStyle), "external reminder style uses supported choreography with its own frames");
            using (FileStream exclusive = File.Open(Path.Combine(folder, "idle", "00.png"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            WritePack(packs, "second-pet", "0.1.0", Color.Purple);
            Require(Until(delegate { return PetCharacter.Find("second-pet").IsExternal; }, 20000), "filesystem events add a new character while app remains running");
            string revision = (string)Field(pet, "loadedCharacterRevision");
            pet.GetType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pet, "waiting");
            pet.GetType().GetField("frameIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pet, 3);
            WritePack(packs, "test-walker", "0.2.0", Color.CornflowerBlue);
            Require(Until(delegate { return (string)Field(pet, "loadedCharacterRevision") != revision && CurrentPixel(pet, Color.CornflowerBlue); }, 20000), "selected character updates in place without restart or reinstall");
            Require((string)Field(pet, "state") == "waiting" && (int)Field(pet, "frameIndex") == 3, "hot update preserves in-progress task reaction and animation phase");
            revision = (string)Field(pet, "loadedCharacterRevision");
            string frame = Path.Combine(folder, "idle", "00.png"); byte[] original = File.ReadAllBytes(frame);
            File.WriteAllBytes(frame, new byte[] { 1, 2, 3 });
            Require(Until(delegate { return ((string[])Field(pet, "characterPackErrors")).Length > 0; }, 20000), "background validation reports a damaged live update");
            Require((string)Field(pet, "loadedCharacterRevision") == revision && CurrentPixel(pet, Color.CornflowerBlue), "damaged update never disposes the visible last-good character");
            File.WriteAllBytes(frame, original);
            Require(Until(delegate { return ((string[])Field(pet, "characterPackErrors")).Length == 0; }, 20000), "fixed pack recovers automatically without restart");
            pet.ContextMenuStrip.Show(pet, new Point(0, 0));
            WritePack(packs, "test-walker", "0.3.0", Color.Orange);
            Require(Until(delegate { return Field(pet, "pendingCharacterScan") != null; }, 20000), "completed scan waits while a menu is open");
            Require((string)Field(pet, "loadedCharacterRevision") == revision, "resource update does not interrupt open menu interactions");
            pet.ContextMenuStrip.Close();
            Require(Until(delegate { return CurrentPixel(pet, Color.Orange); }, 20000), "deferred update applies after menu closes");
            Directory.Move(folder, Path.Combine(packs, ".removed-test-walker"));
            Require(Until(delegate { return (string)Field(pet, "currentCharacterId") == "orbit"; }, 20000), "deleting selected pack safely falls back to built-in companion");
            pet.Close();
            Require((bool)Field(pet, "characterPackWatchingDisposed"), "watcher, timer and queued resources are disposed on close");
        }
    }

    private static void TestManualImport(string stateRoot)
    {
        string manualState = Path.Combine(stateRoot, "manual-import");
        string packs = Path.Combine(manualState, "characters");
        string staged = Path.Combine(manualState, "staged");
        var previousLanguage = UiLanguage.Current;
        string previousState = Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");
        Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", manualState);
        UiLanguage.Set("en");
        try
        {
            using (var pet = new PetForm())
            using (var menu = (ToolStripMenuItem)Call(pet, "BuildCharacterMenu", false))
            {
                pet.Show();
                ((FileSystemWatcher)Field(pet, "characterPackWatcher")).EnableRaisingEvents = false;
                string folder = WritePack(staged, "moon-keeper", "0.1.0", Color.MediumPurple, "Moon Keeper");
                Directory.Move(folder, Path.Combine(packs, "moon-keeper"));
                Require(!PetCharacter.Find("moon-keeper").IsExternal, "copied folder is pending with the automatic watcher disabled");
                var refresh = (ToolStripMenuItem)menu.DropDownItems.Find("reloadCharacterPacks", false)[0];
                Require(menu.DropDownItems.Find("openCharacterPackFolder", false).Length == 1 &&
                        refresh.Text == "Refresh character packs", "character menu provides the folder and explicit refresh actions");
                refresh.PerformClick();
                Require((bool)Field(pet, "characterManualRefresh"), "Refresh character packs queues a scan with a user-facing result");
                // Skip opening the modal dialog in this headless fixture. Verify
                // the report contents after running the actual menu action.
                pet.GetType().GetField("characterManualRefresh", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pet, false);
                Require(Until(delegate { return PetCharacter.Find("moon-keeper").IsExternal; }, 20000),
                        "manual refresh imports a copied folder without restart or a filesystem event");
                Require(((string)Call(pet, "BuildCharacterPackReport")).Contains("Moon Keeper (moon-keeper)"),
                        "refresh results list the imported companion by name and ID");
                Call(pet, "PopulateCharacterMenu", menu, false);
                foreach (ToolStripItem item in menu.DropDownItems)
                {
                    if (!Object.Equals(item.Tag, "moon-keeper")) continue;
                    Require(item.Text == "Moon Keeper (custom)", "new custom pet is labeled in the character menu");
                    item.PerformClick();
                    break;
                }
                Require((string)Field(pet, "currentCharacterId") == "moon-keeper" && CurrentPixel(pet, Color.MediumPurple),
                        "selecting the imported menu item switches to its animation frames");
                // Isolated screenshot for the import guide, without user paths.
                foreach (string language in new[] { "en", "zh-CN" })
                {
                    UiLanguage.Set(language);
                    Call(pet, "PopulateCharacterMenu", menu, false);
                    menu.DropDown.Show(new Point(SystemInformation.VirtualScreen.Right + 120, 0));
                    Application.DoEvents();
                    using (var bitmap = new Bitmap(menu.DropDown.Width, menu.DropDown.Height))
                    {
                        menu.DropDown.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(stateRoot, "character-pack-menu-" + language + ".png"), ImageFormat.Png);
                    }
                    menu.DropDown.Close();
                }
                pet.Close();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", previousState);
            UiLanguage.Set(previousLanguage);
        }
    }

    private static void WriteArchive(string folder, string zip, string prefix, string extraEntry)
    {
        using (var file = File.Create(zip))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string relative = path.Substring(folder.Length + 1).Replace('\\', '/');
                using (Stream entry = archive.CreateEntry(prefix + relative).Open())
                using (Stream input = File.OpenRead(path)) input.CopyTo(entry);
            }
            if (extraEntry != null)
                using (Stream entry = archive.CreateEntry(extraEntry).Open()) entry.WriteByte(1);
        }
    }

    private static void UsePreview(Action open, string button, bool confirm, bool expectFlight = false)
    {
        bool found = false;
        string diagnostics = "";
        DateTime deadline = DateTime.UtcNow.AddSeconds(90);
        using (var timer = new System.Windows.Forms.Timer { Interval = 30 })
        {
            timer.Tick += delegate
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (!(form is CharacterPackPreviewForm) && !(form is CharacterPackCollectionForm)) continue;
                    var action = (Button)form.Controls.Find(form is CharacterPackCollectionForm ? "installCharacterCollection" : "installCharacterPack", true)[0];
                    if (form is CharacterPackCollectionForm)
                    {
                        diagnostics = form.Controls.Find("characterCollectionStatus", true)[0].Text;
                        if (diagnostics.StartsWith(UiLanguage.Text("无法准备角色包。"), StringComparison.Ordinal))
                        { timer.Stop(); form.Close(); return; }
                    }
                    if (action.Enabled && action.Text == button)
                    {
                        if (expectFlight)
                        {
                            ComboBox views = null;
                            foreach (Control control in form.Controls)
                                if (control is ComboBox) views = (ComboBox)control;
                            if (views == null || views.Items.Count != 4) { timer.Stop(); form.Close(); return; }
                            views.SelectedIndex = 3;
                        }
                        found = true; timer.Stop();
                        if (confirm) action.PerformClick(); else form.Close();
                        return;
                    }
                    if (DateTime.UtcNow > deadline) { timer.Stop(); form.Close(); return; }
                }
            };
            timer.Start();
            open();
        }
        Require(found, "preview displays " + button + (found || diagnostics.Length == 0 ? "" : ": " + diagnostics));
    }

    private static void WriteCollection(string zip, string first, string second, string extra)
    {
        using (var file = File.Create(zip))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            foreach (string folder in new[] { first, second })
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string relative = path.Substring(folder.Length + 1).Replace('\\', '/');
                    using (Stream output = archive.CreateEntry(Path.GetFileName(folder) + "/" + relative).Open())
                    using (Stream input = File.OpenRead(path)) input.CopyTo(output);
                }
            if (extra != null) using (Stream output = archive.CreateEntry(extra).Open()) output.WriteByte(1);
        }
    }

    private static void TestCollectionImport(string stateRoot)
    {
        string state = Path.Combine(stateRoot, "collection-import");
        string packs = Path.Combine(state, "characters");
        string source = Path.Combine(state, "source");
        string first = WritePack(source, "batch-one", "1.0.0", Color.Blue);
        string second = WritePack(source, "batch-two", "1.0.0", Color.Green);
        string zip = Path.Combine(state, "collection.zip");
        var token = System.Threading.CancellationToken.None;
        foreach (string extra in new[] { "batch-two/../escape.png", "batch-two/IDLE/00.PNG", "loader.exe" })
        {
            WriteCollection(zip, first, second, extra);
            bool rejected = false;
            try { using (CharacterPackDraft.Collection.Prepare(packs, zip, token)) { } }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected && Directory.GetDirectories(packs).Length == 0, "batch rejects " + extra + " without installing anything");
        }
        File.WriteAllText(Path.Combine(second, "idle", "00.png"), "broken");
        WriteCollection(zip, first, second, null);
        bool corrupt = false;
        try { using (CharacterPackDraft.Collection.Prepare(packs, zip, token)) { } }
        catch (InvalidDataException) { corrupt = true; }
        Require(corrupt && Directory.GetDirectories(packs).Length == 0, "a corrupt later member leaves no partial installation or staging");
        second = WritePack(source, "batch-two", "1.0.0", Color.Green);
        WriteCollection(zip, first, second, "README.md");
        Require(CharacterPackDraft.IsCollection(zip), "the shared ZIP entry identifies a collection");
        using (CharacterPackDraft.Collection.Prepare(packs, zip, token)) { }
        Require(Directory.GetDirectories(packs).Length == 0, "cancelling the collection preview leaves no packs");
        string blockingFile = Path.Combine(packs, "batch-two");
        using (var batch = CharacterPackDraft.Collection.Prepare(packs, zip, token))
        {
            File.WriteAllText(blockingFile, "must survive rollback");
            bool failed = false;
            try { batch.Install(false); }
            catch (IOException) { failed = true; }
            Require(failed && !Directory.Exists(Path.Combine(packs, "batch-one")) &&
                File.ReadAllText(blockingFile) == "must survive rollback", "publish failure rolls back the earlier character and preserves unrelated data");
            File.Delete(blockingFile);
        }
        using (var batch = CharacterPackDraft.Collection.Prepare(packs, zip, token)) batch.Install(false);
        Require(Directory.GetDirectories(packs).Length == 2, "one operation installs both validated characters");
        using (var batch = CharacterPackDraft.Collection.Prepare(packs, zip, token))
        {
            bool blocked = false;
            try { batch.Install(false); }
            catch (IOException) { blocked = true; }
            Require(blocked && batch.WillReplace("batch-one") && batch.WillReplace("batch-two"), "existing characters require explicit confirmation before replacement");
            batch.Install(true);
        }
        Require(Directory.GetDirectories(packs).Length == 2, "confirmed replacement removes staging but preserves both installed characters");
        for (int index = 0; index < 15; index++) Directory.CreateDirectory(Path.Combine(packs, "occupied-" + index));
        bool exceeded = false;
        try { using (CharacterPackDraft.Collection.Prepare(packs, zip, token)) { } }
        catch (IOException) { exceeded = true; }
        Require(exceeded && Directory.GetDirectories(packs).Length == 17, "capacity check refuses the collection without altering existing folders");
    }

    private static void TestZipImport(string stateRoot)
    {
        string state = Path.Combine(stateRoot, "zip-import");
        string packs = Path.Combine(state, "characters");
        string source = WritePack(Path.Combine(state, "source"), "zip-pet", "0.1.0", Color.MediumPurple, "ZIP Pet");
        string first = Path.Combine(state, "first.zip");
        WriteArchive(source, first, "zip-pet/", null);
        string invalid = Path.Combine(state, "invalid.zip");
        foreach (string extra in new[] { "zip-pet/../escape.txt", "zip-pet/IDLE/00.PNG", "zip-pet/plugin.dll" })
        {
            WriteArchive(source, invalid, "zip-pet/", extra);
            bool rejected = false;
            try { using (CharacterPackDraft.FromZip(packs, invalid, System.Threading.CancellationToken.None)) { } }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "ZIP rejects " + extra);
        }
        string frame = Path.Combine(source, "idle", "00.png");
        File.WriteAllText(frame, "not a frame");
        WriteArchive(source, invalid, "", null);
        bool damagedRejected = false;
        try { using (CharacterPackDraft.FromZip(packs, invalid, System.Threading.CancellationToken.None)) { } }
        catch (InvalidDataException) { damagedRejected = true; }
        Require(damagedRejected && Directory.GetDirectories(packs).Length == 0,
            "damaged ZIP resources fail verification and leave no partial pack");

        source = WritePack(Path.Combine(state, "source"), "zip-pet", "0.2.0", Color.Salmon, "ZIP Pet");
        string replacement = Path.Combine(state, "replacement.zip");
        WriteArchive(source, replacement, "", null);
        string previousState = Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");
        string previousLanguage = UiLanguage.Current;
        Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", state);
        UiLanguage.Set("en");
        try
        {
            using (var pet = new PetForm())
            using (var menu = (ToolStripMenuItem)Call(pet, "BuildCharacterMenu", false))
            {
                pet.Show();
                ((System.Windows.Forms.Timer)Field(pet, "timer")).Stop();
                ((FileSystemWatcher)Field(pet, "characterPackWatcher")).EnableRaisingEvents = false;
                Require(menu.DropDownItems.Find("importCharacterPack", false)[0].Text == "Import character pack…",
                    "the character menu exposes the ZIP picker entry");
                UsePreview(delegate { pet.PreviewCharacterPackArchive(first); }, "Install and switch", false);
                Require(!Directory.Exists(Path.Combine(packs, "zip-pet")) && Directory.GetDirectories(packs).Length == 0,
                    "cancelling a valid preview does not install or leave staging files");
                UsePreview(delegate { pet.PreviewCharacterPackArchive(first); }, "Install and switch", true);
                Require(Until(delegate { return (string)Field(pet, "currentCharacterId") == "zip-pet"; }, 20000) &&
                        CurrentPixel(pet, Color.MediumPurple) && pet.LayeredRenderSucceeded,
                    "confirming ZIP preview installs and selects genuine pack frames without a filesystem event");
                UsePreview(delegate { pet.PreviewCharacterPackArchive(replacement); }, "Replace and switch", false);
                Require(CurrentPixel(pet, Color.MediumPurple), "dismissing replacement leaves the installed pet active");
                UsePreview(delegate { pet.PreviewCharacterPackArchive(replacement); }, "Replace and switch", true);
                Require(Until(delegate { return CurrentPixel(pet, Color.Salmon); }, 20000) &&
                        Directory.GetDirectories(packs).Length == 1,
                    "flat ZIP replaces explicitly, switches to the new frames, and cleans staging");
                string suppliedZip = Environment.GetEnvironmentVariable("AGENT_PAL_TEST_CHARACTER_ZIP");
                if (!String.IsNullOrEmpty(suppliedZip))
                {
                    UsePreview(delegate { pet.PreviewCharacterPackArchive(suppliedZip); }, "Install and switch", true);
                    Require(Until(delegate { return (string)Field(pet, "currentCharacterId") == "shinchan"; }, 20000) &&
                        pet.LayeredRenderSucceeded && File.Exists(Path.Combine(packs, "shinchan", "character.json")),
                        "real Shinchan ZIP installs, selects, and renders through the same preview flow");
                }
                string familyZip = Environment.GetEnvironmentVariable("AGENT_PAL_TEST_CHARACTER_COLLECTION");
                if (!String.IsNullOrEmpty(familyZip))
                {
                    string selected = (string)Field(pet, "currentCharacterId");
                    UsePreview(delegate { pet.PreviewCharacterPackArchive(familyZip); }, suppliedZip == null ? "Install all" : "Install all and replace existing", true);
                    string[] ids = { "shinchan", "shiro", "kazama", "masao", "bochan", "nene", "ai-chan", "himawari", "nanako", "kuroiso" };
                    Require(Until(delegate { return Array.TrueForAll(ids, delegate(string id) { return PetCharacter.Find(id).Id == id; }); }, 90000) &&
                        (string)Field(pet, "currentCharacterId") == selected && ((string[])Field(pet, "characterPackErrors")).Length == 0,
                        "one confirmation installs and refreshes all ten real character packs without changing the selection");
                    Call(pet, "PopulateCharacterMenu", menu, false);
                    foreach (string id in ids)
                    {
                        ToolStripItem item = null;
                        foreach (ToolStripItem candidate in menu.DropDownItems)
                            if (Object.Equals(candidate.Tag, id)) { item = candidate; break; }
                        Require(item != null, id + " is available from the normal character menu");
                        item.PerformClick();
                        Require((string)Field(pet, "currentCharacterId") == id && pet.LayeredRenderSucceeded, id + " loads and renders in the desktop pet");
                    }
                }
                pet.Close();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", previousState);
            UiLanguage.Set(previousLanguage);
        }
    }

    private static void DrawView(string path, Color fill, bool jpeg, int nose)
    {
        using (var image = new Bitmap(140, 170, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(image))
        using (var brush = new SolidBrush(fill))
        {
            g.Clear(jpeg ? Color.White : Color.Transparent);
            g.FillEllipse(brush, 43, 20, 52, 55);
            g.FillRectangle(brush, 43, 59, 52, 65);
            g.FillRectangle(brush, 43, 122, 20, 34);
            g.FillRectangle(brush, 76, 122, 20, 34);
            if (nose != 0) g.FillRectangle(brush, nose < 0 ? 28 : 85, 47, 25, 11);
            image.Save(path, jpeg ? ImageFormat.Jpeg : ImageFormat.Png);
        }
    }

    private static void TestImageWizard(string stateRoot)
    {
        const string description = "A quiet courier who speaks in short, precise sentences.";
        string state = Path.Combine(stateRoot, "image-wizard");
        string front = Path.Combine(state, "front.png"), left = Path.Combine(state, "left.png"), right = Path.Combine(state, "right.jpg");
        Directory.CreateDirectory(state);
        DrawView(front, Color.Red, false, 0);
        DrawView(left, Color.Lime, false, -1);
        DrawView(right, Color.Blue, true, 1);
        using (var wizard = new CharacterFromImagesForm())
        {
            Button next = (Button)wizard.Controls.Find("previewCreatedCharacter", true)[0];
            ((TextBox)wizard.Controls.Find("characterName", true)[0]).Text = "Three Views";
            ((TextBox)wizard.Controls.Find("characterDescription", true)[0]).Text = description;
            wizard.SetViewImage(0, front);
            wizard.SetViewImage(1, left);
            Require(!next.Enabled, "wizard requires the right view as well as the front and left");
            wizard.SetViewImage(2, right);
            Require(next.Enabled && wizard.FrontPath == front && wizard.LeftPath == left && wizard.RightPath == right,
                "three required view selections enable the preview");
            wizard.SetArtifactImage(front, true);
            Require(wizard.ArtifactIsWings && wizard.ArtifactPath == front && next.Enabled,
                "the optional wings image needs no other setup");
            Require(wizard.CharacterDescription == description, "the wizard accepts an optional character description");
        }
        string previousState = Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");
        string previousLanguage = UiLanguage.Current;
        Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", state);
        UiLanguage.Set("en");
        try
        {
            using (var pet = new PetForm())
            using (var menu = (ToolStripMenuItem)Call(pet, "BuildCharacterMenu", false))
            {
                pet.Show();
                ((System.Windows.Forms.Timer)Field(pet, "timer")).Stop();
                ((FileSystemWatcher)Field(pet, "characterPackWatcher")).EnableRaisingEvents = false;
                Require(menu.DropDownItems.Find("createCharacterFromImages", false)[0].Text == "Create from images…",
                    "the character menu exposes the image wizard");
                UsePreview(delegate { pet.PreviewCreatedCharacter("Three Views", front, left, right, front, true, description); },
                    "Install and switch", true, true);
                Require(Until(delegate { return ((string)Field(pet, "currentCharacterId")).StartsWith("custom-", StringComparison.Ordinal); }, 20000),
                    "wizard output installs and switches without a restart or watcher");
                PetCharacter profile = PetCharacter.Find((string)Field(pet, "currentCharacterId"));
                CharacterPackManifest manifest = Manifest(profile.ResourceRoot);
                var frames = (Dictionary<string, List<Image>>)Field(pet, "animations");
                Color frontPixel = ((Bitmap)frames["idle"][0]).GetPixel(188, 210);
                Color leftPixel = ((Bitmap)frames["running-left"][0]).GetPixel(188, 210);
                Color rightPixel = ((Bitmap)frames["running-right"][0]).GetPixel(188, 210);
                // Sample beside the torso, inside the canvas but outside the
                // subject and far from the antialiased JPEG edge.
                Color cleared = ((Bitmap)frames["running-right"][0]).GetPixel(100, 245);
                Require(profile.Name == "Three Views" && manifest.quotes.Length == 5 && manifest.files.Count == 241 && frames.Count == 12 &&
                    profile.ArtifactResourceFile == CharacterPackCatalog.WingsFile && Field(pet, "packedFlightArtwork") != null,
                    "wizard installs a standard pack with 240 frames and the verified wings image");
                Require(manifest.persona == description && PetBrain.BuildConversationPrompt(profile, "Hello").Contains(description),
                    "the custom description survives installation and is used in conversation");
                string packedImage = Path.Combine(profile.ResourceRoot, CharacterPackCatalog.WingsFile);
                byte[] intact = File.ReadAllBytes(packedImage);
                File.WriteAllBytes(packedImage, new byte[] { 1, 2, 3 });
                MustReject(profile.ResourceRoot, "changing the packed wings image without its hash rejects the update");
                File.WriteAllBytes(packedImage, intact);
                string exportZip = Path.Combine(state, "wings.zip");
                WriteArchive(profile.ResourceRoot, exportZip, profile.Id + "/", null);
                UsePreview(delegate { pet.PreviewCharacterPackArchive(exportZip); }, "Replace and switch", false, true);
                Require(frontPixel.R > 225 && leftPixel.G > 225 && rightPixel.B > 225 &&
                        leftPixel.R < 25 && rightPixel.R < 25 && cleared.A == 0 && pet.LayeredRenderSucceeded,
                    "front, left, and right each use their supplied view, and the solid JPEG background is removed");
                string sampleFront = Environment.GetEnvironmentVariable("AGENT_PAL_TEST_GAIT_FRONT");
                string sampleLeft = Environment.GetEnvironmentVariable("AGENT_PAL_TEST_GAIT_LEFT");
                string sampleRight = Environment.GetEnvironmentVariable("AGENT_PAL_TEST_GAIT_RIGHT");
                if (File.Exists(sampleFront) && File.Exists(sampleLeft) && File.Exists(sampleRight))
                {
                    UsePreview(delegate { pet.PreviewCreatedCharacter("Long Robe Walk", sampleFront, sampleLeft, sampleRight); },
                        "Install and switch", true);
                    Require(Until(delegate { return PetCharacter.Find((string)Field(pet, "currentCharacterId")).Name == "Long Robe Walk"; }, 20000)
                        && pet.LayeredRenderSucceeded && pet.ValidateLayeredAnimationAdvance(),
                        "the supplied three-view character installs through preview and animates in the layered window");
                    foreach (string gait in new[] { "running-left", "running-right" })
                    {
                        for (int phase = 0; phase < frames[gait].Count; phase++)
                        {
                            Bitmap frame = (Bitmap)frames[gait][phase];
                            int contact = 0;
                            for (int x = 0; x < frame.Width; x++) if (frame.GetPixel(x, 391).A > 128) contact++;
                            if (contact < 4) throw new Exception(gait + " has no grounded sole in phase " + phase);
                        }
                        Require(frames[gait].Count == 24, gait + " supplied art keeps at least one sole down through all 24 phases");
                    }
                }
                pet.Close();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", previousState);
            UiLanguage.Set(previousLanguage);
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if ((args.Length != 1 && !(args.Length==2&&args[1]=="--management")) || !Path.GetFileName(args[0]).StartsWith("agent-pal-character-pack-test-", StringComparison.Ordinal)) throw new Exception("isolated fixture directory required");
            string root = Path.GetFullPath(args[0]), state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
            Environment.SetEnvironmentVariable("AGENT_PAL_TEST_MODE", "1"); Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", state);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==1){TestValidation(Path.Combine(root, "validation")); TestLive(state); TestManualImport(state); TestCollectionImport(state); TestZipImport(state); TestImageWizard(state);}
            TestManagement(Path.Combine(root,"management"));
            WritePack(Path.Combine(root, "export-frames"), "export-demo", "0.1.0", Color.ForestGreen);
            Console.WriteLine("PASS data-only packs and live refresh: no real Claude calls, reminders, startup settings or user files modified.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { PetCharacter.SetExternalProfiles(null); }
    }

    private static void TestManagement(string state)
    {
        string previous=Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");Directory.CreateDirectory(state);Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR",state);
        try
        {
            string root=PetForm.CharacterPackDirectory,folder=WritePack(root,"managed-demo","1.0.0",Color.Teal);
            string history=Path.Combine(state,"keep-history.json");File.WriteAllText(history,"preserved");
            using(var pet=new PetForm())
            {
                pet.Show();Call(pet,"SetCharacter","managed-demo",false);Require((string)Field(pet,"currentCharacterId")=="managed-demo","imported character selected for deletion regression");
                var colors=new[]{Color.Red,Color.Gold,Color.Coral,Color.Brown};CompanionAppearance.Save("managed-demo",colors);Call(pet,"RefreshTrailAppearance");
                Require(CompanionAppearance.Colors("managed-demo")[2].ToArgb()==Color.Coral.ToArgb()&&CompanionAppearance.HasCustom("managed-demo"),"custom trail palette persists per companion outside its pack");
                Require(((Color[])Field(pet,"trailColors"))[0].ToArgb()==Color.Red.ToArgb()&&!CompanionAppearance.HasCustom("orbit"),"active imported trail uses its palette without changing other companions");
                using(var manager=new CompanionManagerForm(()=>(string)Field(pet,"currentCharacterId"),p=>Call(pet,"SetCharacter",p.Id,false),p=>Call(pet,"DeleteImportedCompanion",p),()=>Call(pet,"RefreshTrailAppearance")))
                {
                    manager.Show(pet);Application.DoEvents();var list=(ListBox)Field(manager,"list");
                    Require(((PetCharacter)list.Items[list.Items.Count-1]).Id=="managed-demo"&&((Button)Field(manager,"colors")).Enabled,"management lists imports last and enables their trail editor");
                    using(var image=new Bitmap(manager.Width,manager.Height)){manager.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(state,"companion-manager.png"));}
                    list.SelectedIndex=0;Require(!((Button)Field(manager,"remove")).Enabled,"management disables deletion for built-in companions");manager.Close();
                }
                using(var editor=new TrailColorForm(PetCharacter.Find("managed-demo")))
                {editor.Show(pet);Application.DoEvents();Require(editor.Controls.Find("TrailColor0",true).Length==1,"custom trail editor exposes editable color swatches");using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(state,"trail-colors.png"));}editor.Close();}
                bool rejected=false;try{CharacterPackRemoval.Remove(root,PetCharacter.Find("orbit"));}catch(InvalidOperationException){rejected=true;}Require(rejected,"built-in deletion is rejected");
                var wrong=new PetCharacter{Id="managed-demo",ResourceRoot=state};rejected=false;try{CharacterPackRemoval.Remove(root,wrong);}catch(InvalidDataException){rejected=true;}Require(rejected&&File.Exists(history),"out-of-directory deletion rejected before any mutation");
                string extra=Path.Combine(folder,"personal.txt");File.WriteAllText(extra,"keep");rejected=false;try{CharacterPackRemoval.Remove(root,PetCharacter.Find("managed-demo"));}catch(InvalidDataException){rejected=true;}
                Require(rejected&&File.Exists(extra),"unexpected user files protect a resource folder from deletion");File.Delete(extra);
                Call(pet,"DeleteImportedCompanion",PetCharacter.Find("managed-demo"));
                Require(!Directory.Exists(folder)&&(string)Field(pet,"currentCharacterId")=="orbit"&&PetCharacter.Find("managed-demo").Id=="orbit","active imported deletion removes resources and safely falls back to a built-in");
                Require(File.ReadAllText(history)=="preserved"&&CompanionAppearance.HasCustom("managed-demo"),"deletion retains history and user preferences");
                Require(Until(()=>!Directory.Exists(folder)&&PetCharacter.Find("managed-demo").Id=="orbit",5000),"watcher refresh cannot restore a removed character");
                pet.Close();
            }
            var gold=PetBrain.DefaultColors("sun-wukong");Require(gold[0].R>gold[0].B&&gold[2].R>gold[2].G,"Wukong default trail is warm gold and vermilion");
        }
        finally{Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR",previous);PetCharacter.SetExternalProfiles(null);}
    }
}
