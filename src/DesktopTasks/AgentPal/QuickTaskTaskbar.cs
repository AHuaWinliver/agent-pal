// SPDX-License-Identifier: Apache-2.0
using System;
using System.Runtime.InteropServices;

namespace AgentPal
{
    // The taskbar group reads Shell metadata independently of WM_GETICON.
    // Set the icon/name/relaunch command before the window AppUserModelID:
    // https://learn.microsoft.com/windows/win32/properties/props-system-appusermodel-id
    internal static class QuickTaskTaskbar
    {
        private static readonly Guid Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
        [StructLayout(LayoutKind.Sequential)]private struct Key
        {
            internal Guid Format;internal uint Id;
            internal Key(uint id){Format=QuickTaskTaskbar.Format;Id=id;}
        }
        [StructLayout(LayoutKind.Sequential)]private struct Value
        {
            internal ushort Type,Reserved1,Reserved2,Reserved3;
            internal IntPtr Text,Reserved4;
        }
        [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface Store
        {
            [PreserveSig]int GetCount(out uint count);
            [PreserveSig]int GetAt(uint index,out Key key);
            [PreserveSig]int GetValue(ref Key key,out Value value);
            [PreserveSig]int SetValue(ref Key key,ref Value value);
            [PreserveSig]int Commit();
        }
        [DllImport("shell32.dll")]private static extern int SHGetPropertyStoreForWindow(IntPtr window,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out Store store);
        [DllImport("ole32.dll")]private static extern int PropVariantClear(ref Value value);
        private static Store Open(IntPtr window)
        {
            Guid iid=typeof(Store).GUID;Store store;
            Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window,ref iid,out store));return store;
        }
        internal static string Identity(string id){return "AgentPal.QuickTask.Portrait2."+id.ToLowerInvariant();}
        private static void Write(Store store,uint id,string text)
        {
            Key key=new Key(id);Value value=new Value();
            if(text!=null){value.Type=31;value.Text=Marshal.StringToCoTaskMemUni(text);}
            try{int result=store.SetValue(ref key,ref value);if(result<0)throw new COMException("Taskbar property "+id+" (length "+(text==null?0:text.Length)+")",result);}
            finally{if(value.Text!=IntPtr.Zero)Marshal.FreeCoTaskMem(value.Text);}
        }
        internal static bool Apply(IntPtr window,string id,string iconPath,string title,string relaunch)
        {
            Store store=null;
            try
            {
                store=Open(window);
                Write(store,2,relaunch);Write(store,3,iconPath+",0");Write(store,4,title);
                Write(store,5,Identity(id));Marshal.ThrowExceptionForHR(store.Commit());return true;
            }
            catch(COMException error){System.Diagnostics.Trace.TraceWarning("Task editor Shell identity: "+error.Message);if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")=="1")Console.Error.WriteLine("Task editor Shell metadata: "+error);return false;}
            catch(DllNotFoundException){return false;}catch(EntryPointNotFoundException){return false;}
            finally{if(store!=null)Marshal.ReleaseComObject(store);}
        }
        internal static string Read(IntPtr window,uint id)
        {
            Store store=Open(window);Value value=new Value();Key key=new Key(id);
            try{Marshal.ThrowExceptionForHR(store.GetValue(ref key,out value));return value.Type==31?Marshal.PtrToStringUni(value.Text):null;}
            finally{PropVariantClear(ref value);Marshal.ReleaseComObject(store);}
        }
        internal static void Clear(IntPtr window)
        {
            Store store=null;
            try{store=Open(window);foreach(uint id in new uint[]{2,3,4,5})Write(store,id,null);store.Commit();}
            catch(COMException){}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
            finally{if(store!=null)Marshal.ReleaseComObject(store);}
        }
    }
}
