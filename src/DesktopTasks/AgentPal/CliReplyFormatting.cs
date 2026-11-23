// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AgentPal
{
    internal static class CliReplyFormatting
    {
        internal static void Apply(RichTextBox box)
        {
            int position=0;
            bool code=false;
            foreach(string line in box.Text.Split('\n'))
            {
                int length=line.TrimEnd('\r').Length;
                string value=line.Trim();
                bool fence=value.StartsWith("```",StringComparison.Ordinal);
                box.Select(position,length);
                box.SelectionIndent=0;box.SelectionHangingIndent=0;
                box.SelectionBackColor=code||fence?Color.FromArgb(234,238,235):box.BackColor;
                if(code||fence)
                {
                    using(var font=new Font("Consolas",box.Font.Size))box.SelectionFont=font;
                    box.SelectionColor=Color.FromArgb(39,83,68);
                }
                else if(Regex.IsMatch(value,@"^#{1,6}\s")||value.StartsWith("[工具",StringComparison.Ordinal)||value.StartsWith("[执行",StringComparison.Ordinal))
                {
                    using(var font=UiText.Font(box.Font.Size,FontStyle.Bold))box.SelectionFont=font;
                    box.SelectionColor=Color.FromArgb(30,104,75);
                }
                else if(Regex.IsMatch(value,@"^(?:[-*•]|\d+[.)、])\s"))
                {box.SelectionIndent=10;box.SelectionHangingIndent=10;}
                if(fence)code=!code;
                position+=line.Length+1;
            }
            foreach(Match match in Regex.Matches(box.Text,@"\*\*([^*\r\n]+)\*\*"))
            {
                box.Select(match.Index,match.Length);
                using(var font=UiText.Font(box.Font.Size,FontStyle.Bold))box.SelectionFont=font;
            }
            box.Select(box.TextLength,0);
        }
    }
}
