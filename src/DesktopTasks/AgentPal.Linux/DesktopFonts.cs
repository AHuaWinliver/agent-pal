// SPDX-License-Identifier: Apache-2.0
using Avalonia;
using Avalonia.Media;

namespace AgentPal.Linux;

internal static class DesktopFonts
{
    internal const string TextFamily = "avares://agent-pal/Fonts#Noto Sans CJK SC";
    internal const string EmojiFamily = "avares://agent-pal/Fonts#Noto Color Emoji";

    internal static AppBuilder WithDesktopFonts(this AppBuilder builder) => builder.With(new FontManagerOptions
    {
        DefaultFamilyName = TextFamily,
        FontFallbacks = new[]
        {
            new FontFallback { FontFamily = new FontFamily(EmojiFamily) },
            new FontFallback { FontFamily = new FontFamily(TextFamily) }
        }
    });
}
