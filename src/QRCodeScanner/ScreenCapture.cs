using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace QRCodeScanner
{
    internal static class ScreenCapture
    {
        // SRCCOPY | CAPTUREBLT : CAPTUREBLT inclut les fenêtres en surimpression
        // (overlays, fenêtres translucides), là où un simple SRCCOPY les rate.
        private const CopyPixelOperation SourceCopyWithLayered =
            (CopyPixelOperation)(0x00CC0020 | 0x40000000);

        /// <summary>Capture tous les écrans d'un coup, en pixels physiques.</summary>
        public static Bitmap CaptureAllScreens()
        {
            return Capture(SystemInformation.VirtualScreen);
        }

        public static Bitmap Capture(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("Zone d'écran invalide.");

            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    try
                    {
                        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, SourceCopyWithLayered);
                    }
                    catch
                    {
                        // Certains pilotes d'affichage refusent CAPTUREBLT.
                        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    }
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
    }
}
