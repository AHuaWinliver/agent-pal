// SPDX-License-Identifier: Apache-2.0
using System;
using System.Runtime.InteropServices;

namespace AgentPal
{
    [ComImport, Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface INativeExplorerCommand
    {
        [PreserveSig] int GetTitle(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] out string title);
        [PreserveSig] int GetIcon(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] out string icon);
        [PreserveSig] int GetToolTip(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] out string tooltip);
        [PreserveSig] int GetCanonicalName(out Guid id);
        [PreserveSig] int GetState(IntPtr items, [MarshalAs(UnmanagedType.Bool)] bool slow, out uint state);
        [PreserveSig] int Invoke(IntPtr items, IntPtr context);
        [PreserveSig] int GetFlags(out uint flags);
        [PreserveSig] int EnumSubCommands(out IntPtr commands);
    }
    internal static class QuickTaskNativeProbe
    {
        internal static void Verify(QuickTaskOptions options)
        {
            object root=null;
            try
            {
                root=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(options.Folders?"BC365B20-422D-4FEF-B90B-570000001001":"BC365B20-422D-4FEF-B90B-570000001002")));
                var entry=(INativeExplorerCommand)root;string title,icon;uint flags;int count=0;
                foreach(var command in options.Commands)count+=command.Companions.Count;
                Marshal.ThrowExceptionForHR(entry.GetTitle(IntPtr.Zero,out title));Marshal.ThrowExceptionForHR(entry.GetIcon(IntPtr.Zero,out icon));Marshal.ThrowExceptionForHR(entry.GetFlags(out flags));
                if(title!=UiLanguage.Text("发送Agent Pal任务")||String.IsNullOrEmpty(icon)||flags!=(count>1?1u:0u))throw new InvalidOperationException(UiLanguage.Text("原生菜单校验失败：显示内容与当前伙伴绑定不一致。"));
            }
            finally{if(root!=null&&Marshal.IsComObject(root))Marshal.FinalReleaseComObject(root);}
            int slot = 0;
            foreach (QuickTaskCommand command in options.Commands) foreach (QuickTaskBinding pet in command.Companions)
            {
                object native = null;
                try
                {
                    native = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(QuickTaskModern.ClassId(slot, options.Folders ? 0 : 2))));
                    var handler = (INativeExplorerCommand)native; string title, icon; Guid id;
                    Marshal.ThrowExceptionForHR(handler.GetTitle(IntPtr.Zero, out title));
                    Marshal.ThrowExceptionForHR(handler.GetIcon(IntPtr.Zero, out icon));
                    Marshal.ThrowExceptionForHR(handler.GetCanonicalName(out id));
                    if (title != QuickTaskShell.Caption(command, pet) || icon != pet.IconPath) throw new InvalidOperationException(UiLanguage.Text("原生菜单校验失败：显示内容与当前伙伴绑定不一致。"));
                    if (options.Folders) { uint state; Marshal.ThrowExceptionForHR(handler.GetState(IntPtr.Zero, false, out state)); if (state != 0) throw new InvalidOperationException(UiLanguage.Text("原生菜单校验失败：入口未启用。")); }
                }
                finally { if (native != null && Marshal.IsComObject(native)) Marshal.FinalReleaseComObject(native); }
                slot++;
            }
        }
    }
}
