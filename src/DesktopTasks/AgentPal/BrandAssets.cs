// SPDX-License-Identifier: Apache-2.0
using System.Drawing;
using System.Reflection;
namespace AgentPal
{
    internal static class BrandAssets
    {
        private static Image wordmark;
        private static Image mark;
        internal static Image Mark
        {
            get
            {
                if(mark!=null)return mark;
                var assembly=Assembly.GetExecutingAssembly();
                using(var stream=assembly.GetManifestResourceStream("AgentPal.Brand.Mark.png")??assembly.GetManifestResourceStream("AgentPal.Brand.IntroMark.png"))
                {if(stream!=null)using(var image=Image.FromStream(stream))mark=new Bitmap(image);}
                return mark;
            }
        }
        internal static Image Wordmark
        {
            get
            {
                if(wordmark!=null)return wordmark;
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AgentPal.Brand.Wordmark.png"))
                {if(stream!=null)using(var image=Image.FromStream(stream))wordmark=new Bitmap(image);}
                return wordmark;
            }
        }
    }
}
