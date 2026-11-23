// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;

namespace AgentPal
{
    internal sealed partial class ClaudeCliRunner
    {
        internal event Action<string> ProcessReceived;
        private readonly HashSet<string> reportedProcess = new HashSet<string>();

        private void CaptureProcess(Dictionary<string,object> root)
        {
            string type=GetString(root,"type");
            if(type=="stream_event")
            {
                var evt=GetDictionary(root,"event");
                if(GetString(evt,"type")=="content_block_start")ReportProcessBlock(GetDictionary(evt,"content_block"));
            }
            if(type=="assistant"||type=="user")
            {
                var message=GetDictionary(root,"message");
                object content;
                if(message!=null&&message.TryGetValue("content",out content)&&content is IEnumerable&&!(content is string))
                    foreach(object item in (IEnumerable)content)ReportProcessBlock(item as Dictionary<string,object>);
            }
            if(type=="tool_progress")RaiseStatus("Claude 正在执行工具："+GetString(root,"tool_name"));
        }

        private void ReportProcessBlock(Dictionary<string,object> block)
        {
            if(block==null)return;
            string type=GetString(block,"type");
            if(type=="thinking"){RaiseStatus("Claude 正在分析任务…");return;}
            string key=type+":"+GetString(block,type=="tool_result"?"tool_use_id":"id");
            if(type!="tool_use"&&type!="tool_result")return;
            string text;
            if(type=="tool_use")
            {
                var input=GetDictionary(block,"input");
                string detail="";
                foreach(string name in new[]{"command","file_path","path","pattern","description"})
                {
                    string value=GetString(input,name);
                    if(value.Length>0){detail=" · "+UiText.TailCodeUnits(value,240);break;}
                }
                text="[工具] "+GetString(block,"name")+detail;
                key+=detail;
                RaiseStatus("Claude 正在执行工具："+GetString(block,"name"));
            }
            else
            {
                bool failed=GetString(block,"is_error").Equals("True",StringComparison.OrdinalIgnoreCase);
                string result=GetString(block,"content");object content;
                if(block.TryGetValue("content",out content)&&content is IEnumerable&&!(content is string))
                {
                    var parts=new List<string>();
                    foreach(object item in (IEnumerable)content)
                    {var part=item as Dictionary<string,object>;if(GetString(part,"type")=="text")parts.Add(GetString(part,"text"));}
                    result=String.Join("\n",parts.ToArray());
                }
                text=(failed?"[工具失败] ":"[工具结果] ")+UiText.TailCodeUnits(result,360);
                RaiseStatus(failed?"Claude 工具执行失败":"Claude 工具已返回，继续处理…");
            }
            if(reportedProcess.Add(key)&&ProcessReceived!=null)ProcessReceived(UiText.Normalize(text));
        }
    }
}
