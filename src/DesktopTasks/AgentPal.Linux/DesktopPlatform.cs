// SPDX-License-Identifier: Apache-2.0
using Avalonia;

namespace AgentPal.Linux;

internal static class DesktopPlatform
{
    // VNC sessions may advertise GLX without a usable context. This option
    // selects CPU rasterization before any native windows are created.
    internal static AppBuilder WithLinuxRendering(this AppBuilder builder)
    {
        string requested = Environment.GetEnvironmentVariable("AGENT_PAL_RENDERER") ?? "auto";
        if (requested != "auto" && requested != "software")
            throw new ArgumentException("AGENT_PAL_RENDERER must be auto or software.");
        return builder.With(new X11PlatformOptions
        {
            RenderingMode = requested == "software"
                ? new[] { X11RenderingMode.Software }
                : new[] { X11RenderingMode.Glx, X11RenderingMode.Software }
        });
    }
}
