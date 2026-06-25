using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace KP2FAChecker.UI
{
    /// <summary>
    /// Builds the plugin's 16x16 menu icon from the real KeePass password-entry
    /// icon as the base image, with a colored shield badge overlaid in the
    /// bottom-right corner to mark it as the plugin's own item. Built in code
    /// rather than embedded as a resource because the .plgx load-time compilation
    /// has unclear support for embedded resource files.
    /// </summary>
    internal static class PluginIcon
    {
        // Amber badge color identifies KP2FAChecker (KPPasskeyChecker uses navy).
        private static readonly Color BadgeColor = Color.FromArgb(0xC4, 0x5C, 0x00);
        private static readonly Color Halo = Color.White;

        /// <summary>
        /// Creates a fresh 16x16 ARGB bitmap from the given KeePass key icon with
        /// an amber shield badge in the bottom-right corner. The caller owns it for
        /// the plugin's lifetime and must dispose it (the menu items hold it as
        /// their Image).
        /// </summary>
        internal static Image Create16(Image baseKey)
        {
            var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                g.DrawImage(baseKey, 0, 0, 16, 16);
                DrawBadge(g);
            }
            return bmp;
        }

        // A small 6x7 shield in the bottom-right (x:9-15, y:8-15), preceded by a
        // white halo ring so it separates cleanly from the underlying key icon.
        private static void DrawBadge(Graphics g)
        {
            // Halo as a slightly enlarged shield path so no rectangular artifact
            // appears when the menu item is highlighted (blue selection background).
            using (var haloPath = new GraphicsPath())
            {
                haloPath.AddLine(8f, 7f, 16f, 7f);
                haloPath.AddLine(16f, 7f, 16f, 12f);
                haloPath.AddBezier(16f, 12f, 16f, 15f, 12f, 16f, 12f, 16f);
                haloPath.AddBezier(12f, 16f, 12f, 16f, 8f, 15f, 8f, 12f);
                haloPath.CloseFigure();

                using (var brush = new SolidBrush(Halo))
                    g.FillPath(brush, haloPath);
            }

            using (var path = new GraphicsPath())
            {
                path.AddLine(9f, 8f, 15f, 8f);
                path.AddLine(15f, 8f, 15f, 12f);
                path.AddBezier(15f, 12f, 15f, 14f, 12f, 15f, 12f, 15f);
                path.AddBezier(12f, 15f, 12f, 15f, 9f, 14f, 9f, 12f);
                path.CloseFigure();

                using (var brush = new SolidBrush(BadgeColor))
                    g.FillPath(brush, path);
            }
        }
    }
}
