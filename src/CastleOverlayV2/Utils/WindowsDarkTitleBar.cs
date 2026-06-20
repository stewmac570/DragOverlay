using System.Runtime.InteropServices;

namespace CastleOverlayV2.Utils
{
    /// <summary>
    /// Force the Windows 10/11 native title bar to render dark for a given window.
    /// Works on Windows 10 1809+ and Windows 11. Silently no-ops on older systems.
    /// </summary>
    public static class WindowsDarkTitleBar
    {
        // DWMWA_USE_IMMERSIVE_DARK_MODE attribute id — 20 on current builds, 19 on 1809–1903.
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void Apply(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            int useDark = 1;
            // Try the modern attribute first; if it fails, try the pre-20H1 one. Errors swallowed.
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
                _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));
        }
    }
}
