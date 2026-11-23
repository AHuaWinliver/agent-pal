// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AgentPal;

internal static class QuickTaskShellTests
{
    [StructLayout(LayoutKind.Sequential)]private struct Rect{internal int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct Appbar{internal uint Size;internal IntPtr Window;internal uint Callback,Edge;internal Rect Area;internal IntPtr Parameter;}
    [DllImport("shell32.dll")]private static extern UIntPtr SHAppBarMessage(uint message,ref Appbar data);
    [DllImport("user32.dll")]private static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindow(string name,string title);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern int SHDefExtractIcon(string path,int index,uint flags,out IntPtr large,out IntPtr small,uint size);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    [STAThread]private static int Main(string[] args)
    {
        try
        {
            if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")!="1")throw new Exception("Isolated state is required.");
            string root=Program.StateDirectory;
            string directory=Path.Combine(root,"中文 & project's folder "+new string('x',70));Directory.CreateDirectory(directory);
            var options=new QuickTaskOptions{Enabled=true};
            var command=new QuickTaskCommand{Name="Taskbar portrait check",Task=""};options.Commands.Add(command);
            foreach(string id in new[]{"orbit","sun-wukong"})
            {
                var character=PetCharacter.Find(id);
                command.Companions.Add(new QuickTaskBinding{Id=id,Name=character.Name,IconPath=QuickCommandsForm.CreateIcon(id,args[1])});
            }
            QuickTaskCommands.Save(options);
            foreach(var binding in command.Companions)
            {
                var start=new ProcessStartInfo(args[0],"--quick-task "+command.Id+" --companion "+binding.Id+" --directory \""+directory+"\"")
                    {UseShellExecute=false,CreateNoWindow=true};
                using(var child=Process.Start(start))
                {
                    IntPtr window=IntPtr.Zero;
                    try
                    {
                        var wait=Stopwatch.StartNew();
                        while(wait.ElapsedMilliseconds<10000&&!child.HasExited){child.Refresh();window=child.MainWindowHandle;if(window!=IntPtr.Zero)break;Thread.Sleep(50);}
                        Check(window!=IntPtr.Zero,"compiled --quick-task entry opens its independent window: "+binding.Id);
                        Check(GetWindow(window,4)==IntPtr.Zero,"actual task editor has no hidden modal owner: "+binding.Id);
                        Check(QuickTaskWindow.IsEditor(window),"real editor remains visible to the pet's input-pause detection");
                        Check(QuickTaskTaskbar.Read(window,5)==QuickTaskTaskbar.Identity(binding.Id),"cross-process Shell group identity matches the assigned companion");
                        string resource=QuickTaskTaskbar.Read(window,3);
                        Check(resource==binding.IconPath+",0","cross-process Shell group uses the companion ICO file");
                        IntPtr large=IntPtr.Zero,small=IntPtr.Zero;
                        try
                        {
                            int extracted=SHDefExtractIcon(resource.Substring(0,resource.Length-2),0,0,out large,out small,(16u<<16)|32u);
                            Check(extracted==0&&large!=IntPtr.Zero&&small!=IntPtr.Zero,"Windows Shell extracts large and small taskbar portraits from that resource");
                            using(var borrowed=Icon.FromHandle(large))using(var image=borrowed.ToBitmap())
                            {int visible=0;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(image.GetPixel(x,y).A>32)visible++;Check(visible>image.Width*image.Height/2,"Shell-extracted taskbar portrait is visibly opaque");}
                        }
                        finally{if(large!=IntPtr.Zero)DestroyIcon(large);if(small!=IntPtr.Zero)DestroyIcon(small);}
                        Check(SendMessage(window,0x7f,new IntPtr(1),IntPtr.Zero)!=IntPtr.Zero,"actual window also publishes a title/task-switching icon");
                        Thread.Sleep(1200);CaptureTaskbar(root,binding.Id);
                        PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero);
                        Check(child.WaitForExit(5000)&&child.ExitCode==0,"closing the real editor exits cleanly without executing a task");
                    }
                    finally{if(!child.HasExited){child.Kill();child.WaitForExit(5000);}}
                }
            }
            Console.WriteLine("PASS real task-window entry, Shell icon extraction and close; no Claude calls.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void CaptureTaskbar(string output,string id)
    {
        var state=new Appbar{Size=(uint)Marshal.SizeOf(typeof(Appbar))};
        if((SHAppBarMessage(4,ref state).ToUInt64()&1)!=0){Console.WriteLine("SKIP taskbar pixels: Explorer uses auto-hide; Shell metadata and extracted icon checks still ran.");return;}
        IntPtr tray=FindWindow("Shell_TrayWnd",null);Rect rect;
        if(tray==IntPtr.Zero||!GetWindowRect(tray,out rect)||rect.Right<=rect.Left||rect.Bottom<=rect.Top){Console.WriteLine("SKIP taskbar capture: no interactive Explorer taskbar.");return;}
        using(var image=new Bitmap(rect.Right-rect.Left,rect.Bottom-rect.Top))
        {using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(rect.Left,rect.Top,0,0,image.Size);image.Save(Path.Combine(output,"taskbar-"+id+".png"));}
    }
}
