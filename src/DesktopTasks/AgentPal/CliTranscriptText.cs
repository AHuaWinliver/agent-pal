// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AgentPal
{
    internal sealed class CliTextSpan
    {
        internal int Start,Length;
        internal bool Code,Strong,Muted;
    }
    internal sealed class CliTextDocument
    {
        internal string Text;
        internal readonly List<CliTextSpan> Spans=new List<CliTextSpan>();
    }
    // Presentation only. ChatMessage.Text retains the unmodified reply for copying/history.
    internal static class CliTranscriptText
    {
        internal static CliTextDocument Render(string source)
        {
            var document=new CliTextDocument();var text=new StringBuilder();bool code=false;
            string[] lines=UiText.Normalize(source??"").Replace("\r\n","\n").Split('\n');
            for(int index=0;index<lines.Length;index++)
            {
                string line=lines[index],trimmed=line.TrimStart();int start=text.Length;
                if(trimmed.StartsWith("```",StringComparison.Ordinal))
                {
                    code=!code;
                    string language=trimmed.Substring(3).Trim();
                    if(code&&language.Length>0){text.Append(language);document.Spans.Add(new CliTextSpan{Start=start,Length=language.Length,Muted=true});}
                }
                else if(code)
                {
                    text.Append("  ").Append(line);
                    document.Spans.Add(new CliTextSpan{Start=start,Length=text.Length-start,Code=true});
                }
                else
                {
                    bool heading=Regex.IsMatch(trimmed,@"^#{1,6}\s+");
                    if(heading)line=Regex.Replace(trimmed,@"^#{1,6}\s+","");
                    else if(Regex.IsMatch(trimmed,@"^[-*]\s+"))line="• "+Regex.Replace(trimmed,@"^[-*]\s+","");
                    int position=0;
                    foreach(Match match in Regex.Matches(line,@"\*\*([^*\r\n]+)\*\*"))
                    {
                        text.Append(line.Substring(position,match.Index-position));int boldStart=text.Length;
                        text.Append(match.Groups[1].Value);
                        document.Spans.Add(new CliTextSpan{Start=boldStart,Length=text.Length-boldStart,Strong=true});
                        position=match.Index+match.Length;
                    }
                    text.Append(line.Substring(position));
                    if(heading)document.Spans.Add(new CliTextSpan{Start=start,Length=text.Length-start,Strong=true});
                }
                if(index<lines.Length-1)text.Append('\n');
            }
            document.Text=text.ToString();return document;
        }
    }
}
