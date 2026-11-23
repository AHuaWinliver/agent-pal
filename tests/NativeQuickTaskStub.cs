// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Web.Script.Serialization;
internal static class NativeQuickTaskStub
{
    private static void Main(string[] args)
    {
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-shell-capture.json"),new JavaScriptSerializer().Serialize(new {Arguments=args,Directory=Environment.CurrentDirectory}));
    }
}
