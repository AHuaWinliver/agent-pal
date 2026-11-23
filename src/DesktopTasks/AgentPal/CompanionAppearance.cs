// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace AgentPal
{
    // User appearance preferences live outside resource packs, so refreshing a
    // pack never overwrites them and no package checksums need to be changed.
    internal static class CompanionAppearance
    {
        private static readonly object gate=new object();
        private static string FilePath {get{return Path.Combine(Program.StateDirectory,"companion-appearance.json");}}
        private static Dictionary<string,string[]> Read()
        {
            try{if(File.Exists(FilePath)&&new FileInfo(FilePath).Length<65536)return new JavaScriptSerializer().Deserialize<Dictionary<string,string[]>>(File.ReadAllText(FilePath))??new Dictionary<string,string[]>();}
            catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}catch(UnauthorizedAccessException){}
            return new Dictionary<string,string[]>();
        }
        internal static bool HasCustom(string id){lock(gate){Color[] colors;string[] values;return id!=null&&Read().TryGetValue(id,out values)&&Parse(values,out colors);}}
        internal static Color[] Colors(string id)
        {
            lock(gate){Color[] colors;string[] values;if(id!=null&&Read().TryGetValue(id,out values)&&Parse(values,out colors))return colors;}
            return PetBrain.DefaultColors(id);
        }
        private static bool Parse(string[] values,out Color[] colors)
        {
            colors=null;if(values==null||values.Length<2||values.Length>6)return false;
            var result=new List<Color>();foreach(string value in values)
            {int rgb;if(value==null||value.Length!=7||value[0]!='#'||!Int32.TryParse(value.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb))return false;result.Add(Color.FromArgb((rgb>>16)&255,(rgb>>8)&255,rgb&255));}
            colors=result.ToArray();return true;
        }
        internal static void Save(string id,Color[] colors)
        {
            if(String.IsNullOrWhiteSpace(id)||id.Length>64)throw new ArgumentException("Invalid companion ID.");
            if(colors!=null&&(colors.Length<2||colors.Length>6))throw new ArgumentException("Choose between two and six colors.");
            lock(gate)
            {
                var all=Read();if(colors==null)all.Remove(id);else all[id]=Array.ConvertAll(colors,c=>"#"+(c.ToArgb()&0xffffff).ToString("X6"));
                Directory.CreateDirectory(Program.StateDirectory);string temp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(all));if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);}
                finally{if(File.Exists(temp))File.Delete(temp);}
            }
        }
    }
}
