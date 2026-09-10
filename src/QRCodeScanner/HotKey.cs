using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace QRCodeScanner
{
    /// <summary>Un raccourci clavier global, décrit par ses modificateurs et sa touche.</summary>
    internal struct HotKey
    {
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;
        public const uint ModWin = 0x0008;
        public const uint ModNoRepeat = 0x4000;

        public uint Modifiers;
        public Keys Key;

        public static readonly HotKey Default = new HotKey { Modifiers = 0, Key = Keys.F9 };

        /// <summary>Accepte « F9 », « Ctrl+Shift+Q », « Alt+S », « Win+F9 »…</summary>
        public static bool TryParse(string text, out HotKey hotKey)
        {
            hotKey = new HotKey();
            if (string.IsNullOrEmpty(text)) return false;

            uint modifiers = 0;
            var key = Keys.None;

            foreach (var rawToken in text.Split('+'))
            {
                var token = rawToken.Trim();
                if (token.Length == 0) continue;

                switch (token.ToLowerInvariant())
                {
                    case "ctrl": case "control": case "ctl": modifiers |= ModControl; continue;
                    case "alt": modifiers |= ModAlt; continue;
                    case "shift": case "maj": modifiers |= ModShift; continue;
                    case "win": case "windows": case "meta": case "super": modifiers |= ModWin; continue;
                }

                // Les chiffres seuls s'appellent D0..D9 dans l'énumération Keys.
                if (token.Length == 1 && token[0] >= '0' && token[0] <= '9')
                    token = "D" + token;

                Keys parsed;
                if (!Enum.TryParse(token, true, out parsed) || parsed == Keys.None)
                    return false;

                key = parsed;
            }

            if (key == Keys.None) return false;

            hotKey = new HotKey { Modifiers = modifiers, Key = key };
            return true;
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            if ((Modifiers & ModControl) != 0) text.Append("Ctrl+");
            if ((Modifiers & ModAlt) != 0) text.Append("Alt+");
            if ((Modifiers & ModShift) != 0) text.Append("Shift+");
            if ((Modifiers & ModWin) != 0) text.Append("Win+");
            text.Append(Key);
            return text.ToString();
        }
    }

    /// <summary>
    /// Fenêtre invisible qui reçoit WM_HOTKEY. RegisterHotKey réserve la touche
    /// pour tout le système : elle fonctionne même quand une autre app a le focus.
    /// </summary>
    internal sealed class HotKeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HotKeyId = 0x4B52; // identifiant libre, propre au process
        private static readonly IntPtr HwndMessage = new IntPtr(-3);

        private bool _registered;

        public event EventHandler Pressed;

        public HotKeyWindow()
        {
            CreateHandle(new CreateParams { Parent = HwndMessage });
        }

        public bool Register(HotKey hotKey)
        {
            Unregister();
            _registered = RegisterHotKey(Handle, HotKeyId, hotKey.Modifiers | HotKey.ModNoRepeat, (uint)hotKey.Key);
            return _registered;
        }

        public void Unregister()
        {
            if (!_registered) return;
            UnregisterHotKey(Handle, HotKeyId);
            _registered = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotKeyId)
            {
                var handler = Pressed;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Unregister();
            DestroyHandle();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
