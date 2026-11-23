using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class IconShortcutTests
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static object Get(object item, string name) { return item.GetType().InvokeMember(name, BindingFlags.GetProperty, null, item, null); }
    private static void Set(object item, string name, object value) { item.GetType().InvokeMember(name, BindingFlags.SetProperty, null, item, new object[] { value }); }
    private static object Shortcut(object shell, string path) { return shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path }); }
    private static void Release(object item) { if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item); }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string app = Path.GetFullPath(args[0]), installer = Path.GetFullPath(args[1]);
            string directory = Path.GetDirectoryName(app);
            Require(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") == "1" && directory.Contains("desktop-package-test"), "isolated installer state required");
            string version = AssemblyName.GetAssemblyName(app).Version.ToString(3);
            string iconPath = Path.Combine(directory, "agent-pal-brand-v2.ico");
            ValidateIcon(iconPath);
            using (Icon embedded = Icon.ExtractAssociatedIcon(app))
            using (Bitmap bitmap = embedded.ToBitmap()) CheckBitmap(bitmap, "application executable embedded icon");
            using (Icon embedded = Icon.ExtractAssociatedIcon(installer))
            using (Bitmap bitmap = embedded.ToBitmap()) CheckBitmap(bitmap, "installer executable embedded icon");

            Assembly appAssembly = Assembly.LoadFrom(app);
            Environment.SetEnvironmentVariable("AGENT_PAL_TEST_ASSETS",Path.Combine(directory,"assets"));
            Type petType=appAssembly.GetType("AgentPal.PetForm");
            using(IDisposable pet=(IDisposable)Activator.CreateInstance(petType,true))
            {
                object tray=petType.GetField("tray",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pet);
                object initial=Get(tray,"Icon");
                foreach(string id in new[]{"orbit","spark","moss","pippin","sun-wukong"})
                {
                    petType.GetMethod("SetCharacter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pet,new object[]{id,false});
                    Require(Object.ReferenceEquals(initial,Get(tray,"Icon")),"switching to "+id+" keeps the same product tray icon");
                }
            }
            Type brand=appAssembly.GetType("AgentPal.BrandAssets");
            using(var wordmark=new Bitmap((Image)brand.GetProperty("Wordmark",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null,null)))
                Require(wordmark.Width==1600&&wordmark.GetPixel(0,0).A==0,"real Oxanium wordmark is embedded without a runtime font dependency");
            MethodInfo render = appAssembly.GetType("AgentPal.AppIcon").GetMethod("CreateBitmap", BindingFlags.Static | BindingFlags.NonPublic);
            List<string> images = new List<string> { Path.Combine(directory, "assets", "idle", "00.png") };
            foreach (string character in Directory.GetDirectories(Path.Combine(directory, "assets", "characters"))) images.Add(Path.Combine(character, "idle", "00.png"));
            foreach (string path in images)
                using (Image source = Image.FromFile(path))
                using (Bitmap bitmap = (Bitmap)render.Invoke(null, new object[] { source, 32 })) CheckBitmap(bitmap, "character portrait badge " + Path.GetDirectoryName(Path.GetDirectoryName(path)),false);
            Require(images.Count == 4, "four companion badges remain available separately from product identity");

            Environment.SetEnvironmentVariable("AGENT_PAL_INSTALL_ROOT", directory);
            Type program = Assembly.LoadFrom(installer).GetType("AgentPal.Installer.Program");
            Require(program.GetMethod("RegisterHooks",BindingFlags.Static|BindingFlags.NonPublic)==null && program.GetMethod("RemoveHooks",BindingFlags.Static|BindingFlags.NonPublic)==null,"installer has no Claude settings mutation methods");
            program.GetMethod("ValidateInstallLocation", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { true });
            Require(File.ReadAllText(Path.Combine(directory, ".agent-pal-install")) == "agent-pal:3", "installation ownership marker validated");
            string linkPath = (string)program.GetProperty("StartMenuShortcut", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
            Require(linkPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "test shortcut cannot touch real Start menu");
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            try
            {
                CheckShortcut(shell, linkPath, app, directory, iconPath);
                MethodInfo create = program.GetMethod("RegisterStartMenuShortcut", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo remove = program.GetMethod("RemoveStartMenuShortcut", BindingFlags.Static | BindingFlags.NonPublic);
                create.Invoke(null, null);
                CheckShortcut(shell, linkPath, app, directory, iconPath);
                Require(Directory.GetFiles(Path.GetDirectoryName(linkPath), "*.lnk").Length == 1, "reinstall updates same shortcut without duplicates or staging leftovers");

                string neighbor = Path.Combine(Path.GetDirectoryName(linkPath), "user-kept-file.txt");
                File.WriteAllText(neighbor, "preserve neighboring user files");
                remove.Invoke(null, null);
                Require(!File.Exists(linkPath) && File.Exists(neighbor), "uninstall removes only owned link and preserves neighboring files");
                create.Invoke(null, null);

                string backup = Path.Combine(directory, "owned-shortcut-test-backup.lnk");
                File.Copy(linkPath, backup, true);
                try
                {
                    object foreign = Shortcut(shell, linkPath);
                    try { Set(foreign, "TargetPath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe")); foreign.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, foreign, null); }
                    finally { Release(foreign); }
                    byte[] original = File.ReadAllBytes(linkPath);
                    bool rejected = false;
                    try { create.Invoke(null, null); } catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
                    Require(rejected && SameBytes(original, File.ReadAllBytes(linkPath)), "foreign same-name shortcut is never overwritten");
                    remove.Invoke(null, null);
                    Require(File.Exists(linkPath) && SameBytes(original, File.ReadAllBytes(linkPath)), "uninstall never removes foreign shortcut");
                }
                finally { File.Copy(backup, linkPath, true); File.Delete(backup); File.Delete(neighbor); }
                CheckShortcut(shell, linkPath, app, directory, iconPath);
            }
            finally { Release(shell); }
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    private static bool SameBytes(byte[] first, byte[] second)
    {
        if (first.Length != second.Length) return false;
        for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
        return true;
    }

    private static void CheckShortcut(object shell, string path, string app, string directory, string icon)
    {
        Require(File.Exists(path), "Chinese Start menu shortcut created");
        object shortcut = Shortcut(shell, path);
        try
        {
            Require(String.Equals(Convert.ToString(Get(shortcut, "TargetPath")), app, StringComparison.OrdinalIgnoreCase), "shortcut launches pet, not installer");
            Require(String.Equals(Convert.ToString(Get(shortcut, "WorkingDirectory")), directory, StringComparison.OrdinalIgnoreCase), "shortcut working directory is installed app");
            Require(Convert.ToString(Get(shortcut, "IconLocation")).StartsWith(icon, StringComparison.OrdinalIgnoreCase), "shortcut uses the current approved product icon");
            Require(Convert.ToString(Get(shortcut, "Description")).Contains("Claude Code"), "shortcut describes Claude Code connection");
        }
        finally { Release(shortcut); }
    }

    private static void CheckBitmap(Bitmap bitmap, string label, bool product=true)
    {
        int orange = 0, contrast = 0, light=0,dark=0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (!product && pixel.A != 255) throw new Exception(label + " has transparent pixel at " + x + "," + y);
                if(pixel.A<128)continue;
                if (pixel.R > pixel.G * 1.5 && pixel.R > pixel.B * 1.5 && pixel.R > 120) orange++;
                if(pixel.R>180&&pixel.G>180&&pixel.B>160)light++;
                if(pixel.R<80&&pixel.G<80&&pixel.B<80)dark++;
                // New art is blue/peach/grey, not the retired green robot.
                // Check readable subject/connector contrast against the opaque badge.
                if (Math.Abs(pixel.R-255)+Math.Abs(pixel.G-250)+Math.Abs(pixel.B-237)>145) contrast++;
            }
        if(product){Require(bitmap.GetPixel(0,0).A<32&&bitmap.GetPixel(bitmap.Width-1,bitmap.Height-1).A<32,label+" preserves rounded exterior corners");Require(bitmap.GetPixel(bitmap.Width/2,bitmap.Height/10).A>240,label+" retains an opaque black badge");Require(orange>0&&light>bitmap.Width*bitmap.Height/25&&dark>bitmap.Width*bitmap.Height/5,label+" has the approved light AP, bold prompt and coral cursor");}
        else Require(orange > 0 && contrast > bitmap.Width*bitmap.Height/20, label + " opaque with visible companion and terminal badge");
    }

    private static void ValidateIcon(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            Require(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == sizes.Length, "ICO contains nine resolution entries");
            uint[] offsets = new uint[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                int width = reader.ReadByte(), height = reader.ReadByte();
                if (width == 0) width = 256; if (height == 0) height = 256;
                Require(width == sizes[i] && height == sizes[i], "ICO size " + sizes[i]);
                reader.ReadByte(); reader.ReadByte(); reader.ReadUInt16(); Require(reader.ReadUInt16() == 32, "32-bit ICO color"); reader.ReadUInt32(); offsets[i] = reader.ReadUInt32();
            }
            for (int i = 0; i < sizes.Length; i++)
            {
                reader.BaseStream.Position = offsets[i] + 40;
                int size=sizes[i],opaque=0,transparent=0;byte[] alpha=new byte[size*size];
                for(int pixel=0;pixel<alpha.Length;pixel++){reader.ReadByte();reader.ReadByte();reader.ReadByte();alpha[pixel]=reader.ReadByte();if(alpha[pixel]>240)opaque++;if(alpha[pixel]==0)transparent++;}
                int stride=((size+31)/32)*4;byte[] mask=reader.ReadBytes(stride*size);
                Require(opaque>size*size/2&&transparent>0,"ICO "+size+" keeps its black badge and rounded transparency");
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(((mask[y*stride+x/8]&(0x80>>(x%8)))!=0)!=(alpha[y*size+x]==0))throw new Exception("ICO alpha and AND mask disagree");
            }
        }
        foreach (int size in sizes)
            using (Icon icon = new Icon(path, new Size(size, size)))
            using (Bitmap bitmap = icon.ToBitmap()) CheckBitmap(bitmap, "decoded ICO " + size);
    }

}
