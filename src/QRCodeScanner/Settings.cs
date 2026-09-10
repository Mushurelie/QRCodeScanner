using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace QRCodeScanner
{
    /// <summary>
    /// Réglages stockés dans un .ini lisible à la main.
    /// Mode portable : si un QRCodeScanner.ini existe à côté de l'exe, c'est lui qui compte.
    /// Sinon on écrit dans %APPDATA% (l'exe peut vivre dans un dossier en lecture seule).
    /// </summary>
    internal sealed class Settings
    {
        public string Hotkey = "F9";
        public bool CopyToClipboard = true;
        public bool ShowResultWindow = true;
        public bool OpenUrlAutomatically = false;
        public bool PlaySound = true;
        public bool ShowNotification = true;

        private static string PortablePath
        {
            get
            {
                var dir = Path.GetDirectoryName(Application.ExecutablePath);
                return Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, "QRCodeScanner.ini");
            }
        }

        private static string RoamingPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "QRCodeScanner");
                return Path.Combine(dir, "QRCodeScanner.ini");
            }
        }

        public static string FilePath
        {
            get { return File.Exists(PortablePath) ? PortablePath : RoamingPath; }
        }

        public static Settings Load()
        {
            var settings = new Settings();
            var path = FilePath;

            if (!File.Exists(path))
            {
                try { settings.Save(); } catch { /* dossier en lecture seule : on garde les defauts */ }
                return settings;
            }

            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[') continue;

                var eq = line.IndexOf('=');
                if (eq <= 0) continue;

                var key = line.Substring(0, eq).Trim().ToLowerInvariant();
                var value = line.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "hotkey": if (value.Length > 0) settings.Hotkey = value; break;
                    case "copytoclipboard": settings.CopyToClipboard = ToBool(value, true); break;
                    case "showresultwindow": settings.ShowResultWindow = ToBool(value, true); break;
                    case "openurlautomatically": settings.OpenUrlAutomatically = ToBool(value, false); break;
                    case "playsound": settings.PlaySound = ToBool(value, true); break;
                    case "shownotification": settings.ShowNotification = ToBool(value, true); break;
                }
            }

            return settings;
        }

        public void Save()
        {
            var path = FilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var lines = new List<string>
            {
                "; Réglages de QR Code Scanner.",
                "; Modifie ce fichier, puis « Recharger les réglages » dans le menu de l'icône.",
                "",
                "[QRCodeScanner]",
                "",
                "; Raccourci global. Exemples : F9  |  Ctrl+Shift+Q  |  Alt+S  |  Win+F9",
                "Hotkey=" + Hotkey,
                "",
                "; Copier le premier résultat dans le presse-papiers (1 ou 0)",
                "CopyToClipboard=" + Bit(CopyToClipboard),
                "",
                "; Afficher la fenêtre de résultat (1 ou 0)",
                "ShowResultWindow=" + Bit(ShowResultWindow),
                "",
                "; Ouvrir le lien dans le navigateur sans rien demander (1 ou 0).",
                "; Laisse à 0 : un QR code peut contenir n'importe quelle adresse.",
                "OpenUrlAutomatically=" + Bit(OpenUrlAutomatically),
                "",
                "; Petit son quand un code est trouvé / non trouvé (1 ou 0)",
                "PlaySound=" + Bit(PlaySound),
                "",
                "; Bulle d'information depuis la zone de notification (1 ou 0)",
                "ShowNotification=" + Bit(ShowNotification),
                ""
            };

            File.WriteAllLines(path, lines, new UTF8Encoding(true));
        }

        private static string Bit(bool value) { return value ? "1" : "0"; }

        private static bool ToBool(string value, bool fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;

            switch (value.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "oui": case "yes": case "on": return true;
                case "0": case "false": case "non": case "no": case "off": return false;
                default:
                    int parsed;
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                        ? parsed != 0
                        : fallback;
            }
        }
    }
}
