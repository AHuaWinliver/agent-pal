// SPDX-License-Identifier: Apache-2.0
using System;
using System.Runtime.InteropServices;

namespace AgentPal
{
    // A window property works across the independently launched Explorer dialog
    // without a persistent marker that could remain after a process crash.
    internal static class QuickTaskWindow
    {
        private const string Marker="agent-pal.quick-task-editor";
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool SetProp(IntPtr window,string name,IntPtr value);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetProp(IntPtr window,string name);
        [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
        // Explorer launches the editor in its own process. Give each companion's
        // editors their own group so the pet's window icon is not replaced by
        // a pinned AgentPal product icon. The main application's identity stays put.
        internal static void SetCompanionIdentity(string id)
        {
            if(!QuickTaskCommands.ValidId(id))return;
            try{SetCurrentProcessExplicitAppUserModelID(QuickTaskTaskbar.Identity(id));}
            catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }
        internal static void Mark(IntPtr window){SetProp(window,Marker,new IntPtr(1));}
        internal static bool IsEditor(IntPtr window){return window!=IntPtr.Zero&&GetProp(window,Marker)!=IntPtr.Zero;}
        internal static bool IsForegroundEditor {get{return IsEditor(GetForegroundWindow());}}
    }
}
