// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AgentPal
{
    internal sealed class CliFailureNotice
    {
        internal string Text,Kind;
        internal bool Retrying;
        private static string Value(Dictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):"";}
        private static Dictionary<string,object> Map(Dictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)?value as Dictionary<string,object>:null;}
        private static string Plain(object value)
        {
            if(value==null)return "";var map=value as Dictionary<string,object>;
            if(map!=null){var pieces=new List<string>();foreach(string key in new[]{"type","message","error","text"}){object item;if(map.TryGetValue(key,out item))pieces.Add(Plain(item));}return String.Join(" · ",pieces.ToArray());}
            if(value is string)return (string)value;var list=value as IEnumerable;if(list==null)return Convert.ToString(value);
            var result=new StringBuilder();foreach(object item in list){if(result.Length>0)result.Append(" · ");result.Append(Plain(item));if(result.Length>2000)break;}return result.ToString();
        }
        internal static CliFailureNotice FromEvent(Dictionary<string,object> root)
        {
            string type=Value(root,"type"),subtype=Value(root,"subtype");
            bool retry=type=="system"&&(subtype=="api_retry"||subtype=="api_error");
            bool failed=type=="error"||(type=="result"&&Value(root,"is_error").Equals("true",StringComparison.OrdinalIgnoreCase))||(type=="assistant"&&(Value(root,"isApiErrorMessage").Equals("true",StringComparison.OrdinalIgnoreCase)||Value(root,"error").Length>0));
            if(type=="stream_event"&&Value(Map(root,"event"),"type")=="error")return FromEvent(Map(root,"event"));
            if(!retry&&!failed)return null;
            object raw;string text=root.TryGetValue("error",out raw)?Plain(raw):"";
            if(text.Length==0)text=Value(root,"result");
            if(text.Length==0&&root.TryGetValue("message",out raw))
            {var message=raw as Dictionary<string,object>;object content;text=message!=null&&message.TryGetValue("content",out content)?Plain(content):Plain(raw);}
            if(text.Length==0&&root.TryGetValue("errors",out raw))text=Plain(raw);
            if(text.Length==0)text=failed?"Claude CLI reported an error.":"Claude CLI is retrying the request.";
            if(retry)
            {
                string attempt=Value(root,"attempt"),max=Value(root,"max_retries"),delay=Value(root,"retry_delay_ms");
                if(attempt.Length==0)attempt=Value(root,"retryAttempt");if(max.Length==0)max=Value(root,"maxRetries");if(delay.Length==0)delay=Value(root,"retryInMs");
                if(attempt.Length>0)text+=" · attempt "+attempt+(max.Length>0?"/"+max:"");
                double ms;if(Double.TryParse(delay,out ms)&&ms>=0&&ms<3600000)text+=" · retry in "+(ms/1000).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"s";
            }
            return Create(text,retry);
        }
        internal static CliFailureNotice FromDiagnostic(string text)
        {
            string clean=Regex.Replace(text??"",@"\x1B\[[0-?]*[ -/]*[@-~]","").Trim();
            bool failed=Regex.IsMatch(clean,@"(?i)(ECONNREFUSED|connection refused|ETIMEDOUT|ENOTFOUND|API\s+Error\s*:|authentication[_ ]error|invalid[_ ]api[_ ]key|rate[_ ]limit[_ ]error|overloaded_error|^Error:|Prompt is too long|context length exceeded)");
            if(!failed)return null;return Create(clean,Regex.IsMatch(clean,@"(?i)(retrying|retry in|重试)"));
        }
        private static CliFailureNotice Create(string text,bool retry)
        {string kind=SectPromptPolicy.Classify(text);return new CliFailureNotice {Text=UiText.TailCodeUnits(text,1600),Retrying=retry,Kind=kind.Length>0?kind:"cli-error"};}
    }
}
