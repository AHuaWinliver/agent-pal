// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using AgentPal;

[ComImport,Guid("00000001-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INativeClassFactory
{
    [PreserveSig]int CreateInstance(IntPtr outer,ref Guid iid,out IntPtr value);
    [PreserveSig]int LockServer([MarshalAs(UnmanagedType.Bool)]bool value);
}
[ComImport,Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INativeObjectWithSite
{
    [PreserveSig]int SetSite(IntPtr site);
    [PreserveSig]int GetSite(ref Guid iid,out IntPtr value);
}
[ComImport,Guid("A88826F8-186F-4987-AADE-EA0CEF8FBFE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INativeCommandEnumeration
{
    [PreserveSig]int Next(uint count,out IntPtr command,out uint fetched);
    [PreserveSig]int Skip(uint count);
    [PreserveSig]int Reset();
    [PreserveSig]int Clone(out IntPtr copy);
}
internal static class NativeShellTests
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32.dll")]private static extern IntPtr GetProcAddress(IntPtr module,string name);
    [DllImport("kernel32.dll")]private static extern bool FreeLibrary(IntPtr module);
    [DllImport("ole32.dll")]private static extern int CoGetClassObject(ref Guid clsid,uint context,IntPtr reserved,ref Guid iid,out IntPtr factory);
    [DllImport("shlwapi.dll",CharSet=CharSet.Unicode)]private static extern bool PathIsRelative(string path);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,out IntPtr item);
    [DllImport("shell32.dll")]private static extern int SHCreateShellItemArrayFromShellItem(IntPtr item,ref Guid iid,out IntPtr array);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int GetClass(ref Guid clsid,ref Guid iid,out IntPtr factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall,CharSet=CharSet.Unicode)]private delegate int CreateSite([MarshalAs(UnmanagedType.LPWStr)]string directory,[MarshalAs(UnmanagedType.Bool)]bool desktop,out IntPtr site);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    [STAThread]private static int Main(string[] args)
    {
        IntPtr module=IntPtr.Zero,item=IntPtr.Zero,array=IntPtr.Zero;object command=null,factory=null;
        const string path=QuickTaskModern.RegistryPath+@"\63";
        bool created=false,hadRoot=false;
        try
        {
            if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")!="1")throw new Exception("Isolated test required.");
            using(var root=Registry.CurrentUser.OpenSubKey(QuickTaskModern.RegistryPath))hadRoot=root!=null;
            using(var occupied=Registry.CurrentUser.OpenSubKey(path))if(occupied!=null)throw new Exception("Reserved test slot is occupied; no menu settings were changed.");
            using(var slot=Registry.CurrentUser.CreateSubKey(path))
            {
                created=true;slot.SetValue("Title","Native companion task 中文");slot.SetValue("Icon",args[2]);slot.SetValue("Executable",args[1]);
                slot.SetValue("Command","test-command");slot.SetValue("Companion","orbit");slot.SetValue("Folders",1);slot.SetValue("Desktop",0);
            }
            module=LoadLibrary(Path.GetFullPath(args[0]));Check(module!=IntPtr.Zero,"native x64 DLL loads with system dependencies only");
            var getClass=(GetClass)Marshal.GetDelegateForFunctionPointer(GetProcAddress(module,"DllGetClassObject"),typeof(GetClass));
            if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_REGISTERED_SHELL")=="1")
            {
                getClass=delegate(ref Guid cls,ref Guid interfaceId,out IntPtr value){return CoGetClassObject(ref cls,4,IntPtr.Zero,ref interfaceId,out value);};
                Console.WriteLine("INFO testing registered out-of-process COM activation, not direct DLL activation");
            }
            Guid clsid=new Guid(QuickTaskModern.ClassId(63,0)),factoryIid=new Guid("00000001-0000-0000-C000-000000000046");IntPtr pointer;
            Marshal.ThrowExceptionForHR(getClass(ref clsid,ref factoryIid,out pointer));factory=Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
            Guid iid=typeof(INativeExplorerCommand).GUID;
            Marshal.ThrowExceptionForHR(((INativeClassFactory)factory).CreateInstance(IntPtr.Zero,ref iid,out pointer));command=Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
            var handler=(INativeExplorerCommand)command;string title,icon;Guid canonical;uint state;
            Marshal.ThrowExceptionForHR(handler.GetTitle(IntPtr.Zero,out title));Marshal.ThrowExceptionForHR(handler.GetIcon(IntPtr.Zero,out icon));Marshal.ThrowExceptionForHR(handler.GetCanonicalName(out canonical));
            Marshal.ThrowExceptionForHR(handler.GetState(IntPtr.Zero,false,out state));
            using(var slot=Registry.CurrentUser.OpenSubKey(path))Console.WriteLine("INFO folder="+slot.GetValue("Folders")+" type="+slot.GetValueKind("Folders")+" exe="+slot.GetValue("Executable")+" exists="+File.Exists(args[1])+" relative="+PathIsRelative(args[1]));
            Console.WriteLine("INFO title="+title+" icon="+icon+" canonical="+canonical+" expected="+clsid+" state="+state);
            Check(title=="Native companion task 中文"&&icon==args[2]&&canonical==clsid&&state==0,"native COM supplies the configured title, icon, stable identity and enabled state");
            string directory=Path.Combine(Program.StateDirectory,"中文 & O'Hara folder");Directory.CreateDirectory(directory);
            Guid shellItem=new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),shellArray=new Guid("B63EA76D-1F85-456F-A19C-48159EFA858B");
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(directory,IntPtr.Zero,ref shellItem,out item));Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(item,ref shellArray,out array));
            string capture=Path.Combine(Path.GetDirectoryName(args[1]),"native-shell-capture.json");
            Marshal.ThrowExceptionForHR(handler.Invoke(array,IntPtr.Zero));
            var wait=Stopwatch.StartNew();while(!File.Exists(capture)&&wait.ElapsedMilliseconds<5000)Thread.Sleep(25);
            Check(File.Exists(capture),"native Invoke launches the selected companion task entry");
            var result=new JavaScriptSerializer().Deserialize<Capture>(File.ReadAllText(capture));
            Check(result.Arguments.Length==6&&result.Arguments[1]=="test-command"&&result.Arguments[3]=="orbit"&&Path.GetFullPath(result.Arguments[5])==directory&&result.Directory==directory,"native task invocation preserves command, companion, Unicode and exact current folder");
            using(var slot=Registry.CurrentUser.OpenSubKey(path,true)){slot.SetValue("Companion","moss");}
            Check(handler.Invoke(array,IntPtr.Zero)<0,"an already-open menu cannot silently invoke a changed pet binding");
            using(var slot=Registry.CurrentUser.OpenSubKey(path,true)){slot.SetValue("Folders",0);}
            Marshal.ThrowExceptionForHR(handler.GetState(IntPtr.Zero,false,out state));Check(state!=0,"disabled native entries are hidden");
            if(args.Length>3)TestBackgrounds(getClass,args[3],directory,capture);
            TestAdaptiveRoot(getClass,args[2],array,capture);
            if(args.Length>3)TestAdaptiveBackground(getClass,args[3],directory);
            Console.WriteLine("Native shell bridge checks passed, without Claude or actual task execution.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally
        {
            if(command!=null&&Marshal.IsComObject(command))Marshal.FinalReleaseComObject(command);
            if(factory!=null&&Marshal.IsComObject(factory))Marshal.FinalReleaseComObject(factory);
            if(array!=IntPtr.Zero)Marshal.Release(array);if(item!=IntPtr.Zero)Marshal.Release(item);if(module!=IntPtr.Zero)FreeLibrary(module);
            if(created)Registry.CurrentUser.DeleteSubKeyTree(path,false);
            if(!hadRoot)using(var root=Registry.CurrentUser.OpenSubKey(QuickTaskModern.RegistryPath))if(root!=null&&root.SubKeyCount==0&&root.ValueCount==0){root.Close();Registry.CurrentUser.DeleteSubKey(QuickTaskModern.RegistryPath,false);}
        }
    }
    private sealed class Capture{public string[] Arguments=new string[0];public string Directory="";}
    private static void TestAdaptiveRoot(GetClass getClass,string expectedIcon,IntPtr items,string capture)
    {
        var saved=new System.Collections.Generic.Dictionary<string,int[]>();object oldIcon=null;bool changedIcon=false,created=false;object factory=null,rootCommand=null;
        using(var registry=Registry.CurrentUser.CreateSubKey(QuickTaskModern.RegistryPath))
        try
        {
            using(var occupied=registry.OpenSubKey("62"))if(occupied!=null)throw new Exception("Adaptive test slot is occupied; refusing to overwrite it.");
            foreach(string name in registry.GetSubKeyNames())if(name!="63")using(var key=registry.OpenSubKey(name,true))
            {saved[name]=new[]{Convert.ToInt32(key.GetValue("Folders",0)),Convert.ToInt32(key.GetValue("Desktop",0))};key.SetValue("Folders",0);key.SetValue("Desktop",0);}
            oldIcon=registry.GetValue("Icon");registry.SetValue("Icon",expectedIcon);changedIcon=true;
            using(var slot=registry.OpenSubKey("63",true)){slot.SetValue("Folders",1);slot.SetValue("Desktop",1);}
            Guid clsid=new Guid("BC365B20-422D-4FEF-B90B-570000001001"),factoryId=typeof(INativeClassFactory).GUID,iid=typeof(INativeExplorerCommand).GUID;IntPtr pointer;
            Marshal.ThrowExceptionForHR(getClass(ref clsid,ref factoryId,out pointer));factory=Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
            Marshal.ThrowExceptionForHR(((INativeClassFactory)factory).CreateInstance(IntPtr.Zero,ref iid,out pointer));rootCommand=Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
            var root=(INativeExplorerCommand)rootCommand;uint flags,state;string title,icon;
            root.GetTitle(IntPtr.Zero,out title);root.GetIcon(IntPtr.Zero,out icon);root.GetFlags(out flags);
            Check(flags==0&&icon==expectedIcon&&!String.IsNullOrWhiteSpace(title),"one binding is a direct command with its companion icon and task title");
            File.Delete(capture);Marshal.ThrowExceptionForHR(root.Invoke(items,IntPtr.Zero));var wait=Stopwatch.StartNew();while(!File.Exists(capture)&&wait.ElapsedMilliseconds<5000)Thread.Sleep(25);
            Check(File.Exists(capture),"single root invokes its bound companion directly without another choice");
            using(var source=registry.OpenSubKey("63"))using(var second=registry.CreateSubKey("62")){created=true;foreach(string name in source.GetValueNames())second.SetValue(name,source.GetValue(name),source.GetValueKind(name));second.SetValue("Command","second-test");second.SetValue("Title","Second companion task");}
            root.GetFlags(out flags);root.GetIcon(IntPtr.Zero,out icon);
            Check(flags==1&&icon==expectedIcon,"multiple bindings expose a flyout with an explicit application icon");
            Check(root.Invoke(items,IntPtr.Zero)<0,"an old single entry cannot silently invoke after another binding is added");
            Marshal.ThrowExceptionForHR(root.EnumSubCommands(out pointer));var enumeration=(INativeCommandEnumeration)Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
            try {uint fetched;int count=0;while(enumeration.Next(1,out pointer,out fetched)==0){Check(pointer!=IntPtr.Zero&&fetched==1,"submenu returns a real companion command");Marshal.Release(pointer);count++;}Check(count==2,"submenu contains exactly the two enabled bindings");enumeration.Reset();enumeration.Skip(2);Check(enumeration.Next(1,out pointer,out fetched)==1&&fetched==0,"submenu enumeration reset and skip preserve the end boundary");}
            finally{Marshal.FinalReleaseComObject(enumeration);}
            using(var key=registry.OpenSubKey("62",true)){key.SetValue("Folders",0);key.SetValue("Desktop",0);}
            root.GetFlags(out flags);Check(flags==0,"removing a binding immediately returns the root to direct mode");
            using(var key=registry.OpenSubKey("63",true)){key.SetValue("Folders",0);key.SetValue("Desktop",0);}
            root.GetState(IntPtr.Zero,false,out state);Check(state!=0,"zero bindings hide the root entry");
        }
        finally
        {
            if(rootCommand!=null)Marshal.FinalReleaseComObject(rootCommand);if(factory!=null)Marshal.FinalReleaseComObject(factory);
            if(created)registry.DeleteSubKeyTree("62",false);
            foreach(var item in saved)using(var key=registry.OpenSubKey(item.Key,true)){key.SetValue("Folders",item.Value[0]);key.SetValue("Desktop",item.Value[1]);}
            if(changedIcon){if(oldIcon==null)registry.DeleteValue("Icon",false);else registry.SetValue("Icon",oldIcon);}
        }
    }
    private static void TestBackgrounds(GetClass getClass,string fixture,string directory,string capture)
    {
        IntPtr module=LoadLibrary(fixture);if(module==IntPtr.Zero)throw new Exception("Cannot load test-only context fixture.");
        try
        {
            var create=(CreateSite)Marshal.GetDelegateForFunctionPointer(GetProcAddress(module,"CreateFolderSite"),typeof(CreateSite));
            foreach(int kind in new[]{1,2})foreach(bool desktop in new[]{false,true})
            {
                using(var slot=Registry.CurrentUser.OpenSubKey(QuickTaskModern.RegistryPath+@"\63",true)){slot.SetValue("Companion","orbit");slot.SetValue("Folders",1);slot.SetValue("Desktop",1);}
                IntPtr site=IntPtr.Zero,pointer;object factory=null,command=null;
                try
                {
                    Guid clsid=new Guid(QuickTaskModern.ClassId(63,kind)),factoryId=typeof(INativeClassFactory).GUID,iid=typeof(INativeExplorerCommand).GUID;
                    Marshal.ThrowExceptionForHR(getClass(ref clsid,ref factoryId,out pointer));factory=Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
                    Marshal.ThrowExceptionForHR(((INativeClassFactory)factory).CreateInstance(IntPtr.Zero,ref iid,out pointer));command=Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
                    Marshal.ThrowExceptionForHR(create(directory,desktop,out site));Marshal.ThrowExceptionForHR(((INativeObjectWithSite)command).SetSite(site));
                    var handler=(INativeExplorerCommand)command;uint state;Marshal.ThrowExceptionForHR(handler.GetState(IntPtr.Zero,false,out state));
                    Check((state==0)==(desktop==(kind==2)),"folder and desktop backgrounds enable exactly the matching native command: "+kind+" / "+desktop);
                    if(kind==1&&!desktop)
                    {
                        File.Delete(capture);Marshal.ThrowExceptionForHR(handler.Invoke(IntPtr.Zero,IntPtr.Zero));
                        var watch=Stopwatch.StartNew();while(!File.Exists(capture)&&watch.ElapsedMilliseconds<5000)Thread.Sleep(25);
                        var result=new JavaScriptSerializer().Deserialize<Capture>(File.ReadAllText(capture));
                        Check(Path.GetFullPath(result.Arguments[5])==directory,"folder-background invocation resolves the actual view directory with no selected items");
                    }
                }
                finally{if(command!=null)Marshal.FinalReleaseComObject(command);if(factory!=null)Marshal.FinalReleaseComObject(factory);if(site!=IntPtr.Zero)Marshal.Release(site);}
            }
        }
        finally{FreeLibrary(module);}
    }
    private static void TestAdaptiveBackground(GetClass getClass,string fixture,string directory)
    {
        IntPtr module=LoadLibrary(fixture);if(module==IntPtr.Zero)throw new Exception("Cannot load background fixture.");
        try
        {
            var create=(CreateSite)Marshal.GetDelegateForFunctionPointer(GetProcAddress(module,"CreateFolderSite"),typeof(CreateSite));
            foreach(bool desktop in new[]{false,true})
            {
                IntPtr site=IntPtr.Zero,pointer;object factory=null,command=null;
                try
                {
                    Guid clsid=new Guid("BC365B20-422D-4FEF-B90B-570000001002"),factoryId=typeof(INativeClassFactory).GUID,iid=typeof(INativeExplorerCommand).GUID;
                    Marshal.ThrowExceptionForHR(getClass(ref clsid,ref factoryId,out pointer));factory=Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
                    Marshal.ThrowExceptionForHR(((INativeClassFactory)factory).CreateInstance(IntPtr.Zero,ref iid,out pointer));command=Marshal.GetObjectForIUnknown(pointer);Marshal.Release(pointer);
                    Marshal.ThrowExceptionForHR(create(directory,desktop,out site));Marshal.ThrowExceptionForHR(((INativeObjectWithSite)command).SetSite(site));
                    using(var slot=Registry.CurrentUser.OpenSubKey(QuickTaskModern.RegistryPath+@"\63",true)){slot.SetValue("Folders",desktop?0:1);slot.SetValue("Desktop",desktop?1:0);}
                    var handler=(INativeExplorerCommand)command;uint state;handler.GetState(IntPtr.Zero,false,out state);Check(state==0,"one background handler supports the enabled context: desktop="+desktop);
                    using(var slot=Registry.CurrentUser.OpenSubKey(QuickTaskModern.RegistryPath+@"\63",true)){slot.SetValue("Folders",desktop?1:0);slot.SetValue("Desktop",desktop?0:1);}
                    bool any=false;using(var all=Registry.CurrentUser.OpenSubKey(QuickTaskModern.RegistryPath))foreach(string name in all.GetSubKeyNames())using(var entry=all.OpenSubKey(name))any|=Convert.ToInt32(entry.GetValue(desktop?"Desktop":"Folders",0))!=0;
                    handler.GetState(IntPtr.Zero,false,out state);Check((state==0)==any,"background root filters commands by the actual desktop/folder context");
                }
                finally{if(command!=null)Marshal.FinalReleaseComObject(command);if(factory!=null)Marshal.FinalReleaseComObject(factory);if(site!=IntPtr.Zero)Marshal.Release(site);}
            }
        }finally{FreeLibrary(module);}
    }
}
