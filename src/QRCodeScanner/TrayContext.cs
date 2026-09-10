using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QRCodeScanner
{
    /// <summary>
    /// Le coeur de l'application : une icône dans la zone de notification, un
    /// raccourci global, et le va-et-vient entre le thread UI et le scan.
    /// </summary>
    internal sealed class TrayContext : ApplicationContext
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "QRCodeScanner";
        private const int MaxHistory = 12;
        private const int MaxTooltipLength = 63; // limite imposée par NotifyIcon

        private readonly Control _uiThread;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly HotKeyWindow _hotKeyWindow;
        private readonly List<string> _history = new List<string>();
        private readonly Icon _icon;

        private readonly ToolStripMenuItem _scanItem;
        private readonly ToolStripMenuItem _historyItem;
        private readonly ToolStripMenuItem _startupItem;

        private Settings _settings;
        private HotKey _hotKey;
        private bool _scanning;

        public TrayContext()
        {
            // Un contrôle bien réel : son handle nous sert à revenir sur le
            // thread UI depuis le thread de scan.
            _uiThread = new Control();
            _uiThread.CreateControl();

            _settings = Settings.Load();

            // SystemInformation renvoie 16 quel que soit le DPI (voir Dpi) : on
            // dessine l'icône à la taille réellement attendue par la barre des tâches.
            _icon = AppIcons.CreateIcon(Math.Max(16, (int)Math.Round(16 * Dpi.DesktopScale)));

            _scanItem = new ToolStripMenuItem("Scanner maintenant", null, (s, e) => BeginScan())
            {
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold)
            };
            _historyItem = new ToolStripMenuItem("Historique");
            _historyItem.DropDownOpening += (s, e) => RebuildHistoryMenu();
            _startupItem = new ToolStripMenuItem("Démarrer avec Windows", null, (s, e) => ToggleStartup())
            {
                CheckOnClick = false,
                Checked = IsStartupEnabled()
            };

            _menu = new ContextMenuStrip();
            _menu.Items.Add(_scanItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_historyItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_startupItem);
            _menu.Items.Add(new ToolStripMenuItem("Ouvrir les réglages...", null, (s, e) => OpenSettingsFile()));
            _menu.Items.Add(new ToolStripMenuItem("Recharger les réglages", null, (s, e) => ReloadSettings()));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("À propos", null, (s, e) => ShowAbout()));
            _menu.Items.Add(new ToolStripMenuItem("Quitter", null, (s, e) => ExitThread()));

            _notifyIcon = new NotifyIcon
            {
                Icon = _icon,
                ContextMenuStrip = _menu,
                Visible = true
            };
            _notifyIcon.DoubleClick += (s, e) => BeginScan();
            _notifyIcon.BalloonTipClicked += (s, e) => ShowLastResult();

            _hotKeyWindow = new HotKeyWindow();
            _hotKeyWindow.Pressed += (s, e) => BeginScan();

            ApplyHotKey();
            RebuildHistoryMenu();

            if (_settings.ShowNotification)
            {
                Notify(Program.AppName + " est prêt",
                    "Appuie sur " + _hotKey + " pour scanner les QR codes affichés à l'écran.",
                    ToolTipIcon.Info);
            }
        }

        // --- Scan ------------------------------------------------------------

        private void BeginScan()
        {
            if (_scanning) return;

            _scanning = true;
            SetTooltip("Scan de l'écran en cours...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> results = null;
                Exception failure = null;

                try
                {
                    using (var screenshot = ScreenCapture.CaptureAllScreens())
                        results = QrDecoder.Scan(screenshot);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                var capturedResults = results;
                var capturedFailure = failure;

                try
                {
                    _uiThread.BeginInvoke(new Action(() => FinishScan(capturedResults, capturedFailure)));
                }
                catch
                {
                    // L'application se ferme pendant le scan : plus rien à afficher.
                }
            });
        }

        private void FinishScan(List<string> results, Exception failure)
        {
            _scanning = false;
            SetTooltip(Program.AppName + " - " + _hotKey);

            if (failure != null)
            {
                Notify("Le scan a échoué", failure.Message, ToolTipIcon.Error);
                return;
            }

            if (results == null || results.Count == 0)
            {
                if (_settings.PlaySound) SystemSounds.Exclamation.Play();
                Notify("Aucun QR code trouvé",
                    "Rien de lisible sur l'écran. Agrandis le code, puis réessaie.",
                    ToolTipIcon.Warning);
                return;
            }

            foreach (var result in results)
            {
                _history.Remove(result);
                _history.Insert(0, result);
            }
            while (_history.Count > MaxHistory)
                _history.RemoveAt(_history.Count - 1);

            if (_settings.PlaySound) SystemSounds.Asterisk.Play();

            var first = results[0];
            var copied = _settings.CopyToClipboard && Util.TrySetClipboardText(first);

            if (_settings.ShowNotification)
            {
                var title = results.Count > 1
                    ? results.Count + " QR codes détectés"
                    : (copied ? "QR code copié" : "QR code détecté");
                Notify(title, Util.SingleLine(first, 120), ToolTipIcon.Info);
            }

            Uri uri;
            if (_settings.OpenUrlAutomatically && Util.TryGetLaunchableUri(first, out uri))
            {
                try { Process.Start(uri.AbsoluteUri); }
                catch { /* pas de navigateur par défaut : on garde la fenêtre */ }
            }

            if (_settings.ShowResultWindow)
                ResultForm.ShowResults(results, _icon);
        }

        private void ShowLastResult()
        {
            if (_history.Count == 0) return;
            ResultForm.ShowResults(new List<string> { _history[0] }, _icon);
        }

        // --- Raccourci -------------------------------------------------------

        private void ApplyHotKey()
        {
            if (!HotKey.TryParse(_settings.Hotkey, out _hotKey))
            {
                _hotKey = HotKey.Default;
                Notify("Raccourci illisible",
                    "« " + _settings.Hotkey + " » n'est pas un raccourci valide. Retour à F9.",
                    ToolTipIcon.Warning);
            }

            _scanItem.Text = "Scanner maintenant (" + _hotKey + ")";
            SetTooltip(Program.AppName + " - " + _hotKey);

            if (!_hotKeyWindow.Register(_hotKey))
            {
                Notify("Raccourci indisponible",
                    _hotKey + " est déjà pris par une autre application. Change Hotkey dans les réglages.",
                    ToolTipIcon.Warning);
            }
        }

        private void ReloadSettings()
        {
            _settings = Settings.Load();
            ApplyHotKey();
            Notify("Réglages rechargés", "Raccourci actif : " + _hotKey, ToolTipIcon.Info);
        }

        private void OpenSettingsFile()
        {
            try
            {
                var path = Settings.FilePath;
                if (!System.IO.File.Exists(path)) _settings.Save();
                Process.Start("notepad.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible d'ouvrir les réglages :" + Environment.NewLine + ex.Message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // --- Menus -----------------------------------------------------------

        private void RebuildHistoryMenu()
        {
            _historyItem.DropDownItems.Clear();

            if (_history.Count == 0)
            {
                _historyItem.DropDownItems.Add(new ToolStripMenuItem("(vide)") { Enabled = false });
                return;
            }

            foreach (var entry in _history)
            {
                var value = entry;
                _historyItem.DropDownItems.Add(new ToolStripMenuItem(
                    Util.SingleLine(value, 60), null,
                    (s, e) => ResultForm.ShowResults(new List<string> { value }, _icon)));
            }

            _historyItem.DropDownItems.Add(new ToolStripSeparator());
            _historyItem.DropDownItems.Add(new ToolStripMenuItem("Effacer l'historique", null,
                (s, e) => _history.Clear()));
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    return key != null && key.GetValue(RunValueName) != null;
            }
            catch
            {
                return false;
            }
        }

        private void ToggleStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;

                    if (IsStartupEnabled())
                    {
                        key.DeleteValue(RunValueName, false);
                        _startupItem.Checked = false;
                    }
                    else
                    {
                        key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                        _startupItem.Checked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de modifier le démarrage automatique :"
                    + Environment.NewLine + ex.Message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowAbout()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            MessageBox.Show(
                Program.AppName + " " + version.ToString(3) + Environment.NewLine + Environment.NewLine
                + "Appuie sur " + _hotKey + " n'importe où dans Windows : l'app capture tous les"
                + " écrans, décode les QR codes visibles et copie le résultat." + Environment.NewLine
                + Environment.NewLine
                + "Réglages : " + Settings.FilePath + Environment.NewLine
                + "Décodage : ZXing.Net",
                "À propos", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // --- Divers ----------------------------------------------------------

        private void SetTooltip(string text)
        {
            _notifyIcon.Text = Util.SingleLine(text, MaxTooltipLength);
        }

        private void Notify(string title, string message, ToolTipIcon icon)
        {
            if (!_settings.ShowNotification && icon == ToolTipIcon.Info) return;

            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(4000);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_hotKeyWindow != null) _hotKeyWindow.Dispose();
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                if (_menu != null) _menu.Dispose();
                if (_icon != null) _icon.Dispose();
                if (_uiThread != null) _uiThread.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
