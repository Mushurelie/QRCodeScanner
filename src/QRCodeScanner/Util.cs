using System;
using System.Threading;
using System.Windows.Forms;

namespace QRCodeScanner
{
    internal static class Util
    {
        /// <summary>
        /// Le presse-papiers est une ressource partagée : une autre app peut le tenir
        /// une fraction de seconde. On réessaie plutôt que d'échouer bêtement.
        /// </summary>
        public static bool TrySetClipboardText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch
                {
                    Thread.Sleep(60);
                }
            }

            return false;
        }

        /// <summary>
        /// Un QR code peut contenir n'importe quoi (file://, des schémas d'app…).
        /// On n'accepte d'ouvrir que ce qui est raisonnablement sûr.
        /// </summary>
        public static bool TryGetLaunchableUri(string text, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrEmpty(text)) return false;

            Uri parsed;
            if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out parsed)) return false;

            if (parsed.Scheme != Uri.UriSchemeHttp
                && parsed.Scheme != Uri.UriSchemeHttps
                && parsed.Scheme != Uri.UriSchemeMailto)
                return false;

            uri = parsed;
            return true;
        }

        public static string SingleLine(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var flat = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            while (flat.Contains("  ")) flat = flat.Replace("  ", " ");

            return flat.Length <= maxLength ? flat : flat.Substring(0, Math.Max(1, maxLength - 1)) + "…";
        }
    }
}
