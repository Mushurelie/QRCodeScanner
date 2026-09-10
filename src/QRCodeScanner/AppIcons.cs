using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QRCodeScanner
{
    /// <summary>
    /// L'icône est dessinée à la volée, à la taille exacte demandée par le système.
    /// Ça reste net quel que soit le DPI, sans embarquer de PNG à chaque taille.
    /// </summary>
    internal static class AppIcons
    {
        private static readonly Color Background = Color.FromArgb(79, 70, 229);
        private static readonly Color Foreground = Color.White;

        // Grille 7x7 : trois repères d'angle (2x2) plus quelques modules,
        // assez simple pour rester lisible à 16 pixels.
        private static readonly Point[] Finders = { new Point(0, 0), new Point(5, 0), new Point(0, 5) };
        private static readonly Point[] Modules =
        {
            new Point(3, 2), new Point(2, 3), new Point(4, 3),
            new Point(3, 4), new Point(5, 4), new Point(4, 5),
            new Point(6, 6), new Point(4, 6)
        };

        public static Bitmap CreateBitmap(int size)
        {
            if (size < 8) size = 8;

            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                var radius = size * 0.24f;
                using (var brush = new SolidBrush(Background))
                using (var path = RoundedRectangle(new RectangleF(0, 0, size, size), radius))
                    graphics.FillPath(brush, path);

                var unit = size / 9f;
                using (var brush = new SolidBrush(Foreground))
                {
                    foreach (var finder in Finders)
                        graphics.FillRectangle(brush,
                            (1 + finder.X) * unit, (1 + finder.Y) * unit, unit * 2, unit * 2);

                    foreach (var module in Modules)
                        graphics.FillRectangle(brush,
                            (1 + module.X) * unit, (1 + module.Y) * unit, unit, unit);
                }
            }

            return bitmap;
        }

        public static Icon CreateIcon(int size)
        {
            using (var bitmap = CreateBitmap(size))
            {
                var handle = bitmap.GetHicon();
                try
                {
                    using (var temporary = Icon.FromHandle(handle))
                        return (Icon)temporary.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
