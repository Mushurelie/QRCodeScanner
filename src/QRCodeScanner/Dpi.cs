using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace QRCodeScanner
{
    /// <summary>
    /// Mise à l'échelle des dimensions, calculée à la main.
    /// <para>
    /// L'application est déclarée PerMonitorV2 dans son manifeste, ce qui est
    /// indispensable pour capturer l'écran en vrais pixels. Mais WinForms .NET
    /// Framework n'active sa gestion du DPI que via un fichier .exe.config, et on
    /// tient à livrer un exe seul : <c>Control.DeviceDpi</c> renvoie donc 96 quoi
    /// qu'il arrive, et <c>AutoScaleMode.Dpi</c> rétrécit les fenêtres au lieu de
    /// les agrandir.
    /// </para>
    /// <para>
    /// En revanche GDI+ rend bien les polices au DPI réel. On multiplie donc
    /// seulement les dimensions en pixels, jamais les tailles de police.
    /// </para>
    /// </summary>
    internal static class Dpi
    {
        private const int MonitorDefaultToNearest = 2;
        private const int EffectiveDpi = 0;
        private const int LogPixelsX = 88;

        /// <summary>Facteur d'échelle de l'écran qui contient ce point (1,0 à 100 %).</summary>
        public static float ScaleForPoint(Point point)
        {
            try
            {
                var monitor = MonitorFromPoint(new NativePoint { X = point.X, Y = point.Y },
                    MonitorDefaultToNearest);

                uint dpiX, dpiY;
                if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, EffectiveDpi, out dpiX, out dpiY) == 0 && dpiX > 0)
                    return dpiX / 96f;
            }
            catch (DllNotFoundException)
            {
                // Shcore.dll n'existe qu'à partir de Windows 8.1.
            }
            catch (EntryPointNotFoundException)
            {
            }

            return DesktopScale;
        }

        /// <summary>Facteur d'échelle du bureau, utilisable avant toute fenêtre.</summary>
        public static float DesktopScale
        {
            get
            {
                var screenDc = GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero) return 1f;

                try
                {
                    var dpi = GetDeviceCaps(screenDc, LogPixelsX);
                    return dpi > 0 ? dpi / 96f : 1f;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, int flags);

        [DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr dc, int index);
    }
}
