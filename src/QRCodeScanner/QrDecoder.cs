using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace QRCodeScanner
{
    internal static class QrDecoder
    {
        // Agrandir une capture 4K coûte des centaines de Mo : au-delà de ce seuil
        // on se contente des passes 1x et 0,5x.
        private const long MaxPixelsForUpscale = 9000000L;

        // 1x couvre la grande majorité des cas ; 0,5x aide sur les très grands QR
        // affichés en plein écran, 2x sur les vignettes minuscules.
        private static readonly double[] Passes = { 1.0, 0.5, 2.0 };

        /// <summary>Cherche des QR codes dans une image. Renvoie les textes trouvés, sans doublon.</summary>
        public static List<string> Scan(Bitmap image)
        {
            if (image == null) throw new ArgumentNullException("image");

            foreach (var scale in Passes)
            {
                if (scale > 1.0 && (long)image.Width * image.Height > MaxPixelsForUpscale)
                    continue;

                Bitmap scaled = null;
                try
                {
                    if (Math.Abs(scale - 1.0) > 0.001)
                    {
                        scaled = Resize(image, scale);
                        if (scaled == null) continue;
                    }

                    var found = DecodeAll(scaled ?? image);
                    if (found.Count > 0) return found;
                }
                finally
                {
                    if (scaled != null) scaled.Dispose();
                }
            }

            return new List<string>();
        }

        public static List<string> ScanFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Image introuvable : " + path);

            // On passe par la mémoire pour ne pas garder le fichier verrouillé.
            var bytes = File.ReadAllBytes(path);
            using (var stream = new MemoryStream(bytes))
            using (var loaded = new Bitmap(stream))
            using (var copy = new Bitmap(loaded.Width, loaded.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(copy))
                    graphics.DrawImageUnscaled(loaded, 0, 0);

                return Scan(copy);
            }
        }

        public static void WriteQrPng(string text, string path, int size)
        {
            var writer = new BarcodeWriterGeneric
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = size,
                    Height = size,
                    Margin = 2,
                    CharacterSet = "UTF-8"
                }
            };

            var matrix = writer.Encode(text);
            using (var bitmap = new Bitmap(matrix.Width, matrix.Height, PixelFormat.Format32bppRgb))
            {
                for (var y = 0; y < matrix.Height; y++)
                    for (var x = 0; x < matrix.Width; x++)
                        bitmap.SetPixel(x, y, matrix[x, y] ? Color.Black : Color.White);

                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private static List<string> DecodeAll(Bitmap image)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            var source = ToLuminanceSource(image);
            var reader = new BarcodeReaderGeneric { AutoRotate = true };
            reader.Options.PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE };
            reader.Options.TryHarder = true;

            var decoded = reader.DecodeMultiple(source);
            if (decoded == null)
            {
                var single = reader.Decode(source);
                decoded = single == null ? null : new[] { single };
            }

            if (decoded != null)
            {
                foreach (var result in decoded)
                {
                    if (result == null || string.IsNullOrEmpty(result.Text)) continue;
                    if (seen.Add(result.Text)) results.Add(result.Text);
                }
            }

            return results;
        }

        private static LuminanceSource ToLuminanceSource(Bitmap image)
        {
            // Les bitmaps 32bppArgb ont une foulée égale à largeur * 4, donc les
            // octets sortent déjà compactés comme ZXing les attend.
            var rectangle = new Rectangle(0, 0, image.Width, image.Height);
            var data = image.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var buffer = new byte[data.Stride * data.Height];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
                return new RGBLuminanceSource(buffer, image.Width, image.Height,
                    RGBLuminanceSource.BitmapFormat.BGRA32);
            }
            finally
            {
                image.UnlockBits(data);
            }
        }

        private static Bitmap Resize(Bitmap image, double scale)
        {
            var width = (int)Math.Round(image.Width * scale);
            var height = (int)Math.Round(image.Height * scale);
            if (width < 8 || height < 8) return null;

            var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            try
            {
                using (var graphics = Graphics.FromImage(resized))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    graphics.DrawImage(image, new Rectangle(0, 0, width, height));
                }
                return resized;
            }
            catch
            {
                resized.Dispose();
                return null;
            }
        }
    }
}
