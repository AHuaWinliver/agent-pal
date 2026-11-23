// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
internal static class QuickTaskStub
{
    private static int Main(string[] args)
    {
        File.WriteAllText(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_QUICK_CAPTURE"),new JavaScriptSerializer().Serialize(new {
            Directory=Environment.CurrentDirectory,Arguments=args,Request=Environment.GetEnvironmentVariable("AGENT_PAL_QUICK_TASK_FILE")
        }),new UTF8Encoding(false));return 0;
    }
}
