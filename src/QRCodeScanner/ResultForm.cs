using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace QRCodeScanner
{
    /// <summary>
    /// Petite fenêtre qui présente le contenu des QR codes trouvés.
    /// Toutes les dimensions sont écrites pour 96 DPI et passées par <see cref="S"/> ;
    /// les polices ne sont pas touchées, GDI+ les rend déjà au bon DPI (voir <see cref="Dpi"/>).
    /// </summary>
    internal sealed class ResultForm : Form
    {
        private const int CardWidth = 452;
        private const int Gutter = 14;
        private const int MinBoxHeight = 44;
        private const int MaxBoxHeight = 132;
        private const int MaxContentHeight = 420;
        private const int ActionsHeight = 30;
        private const int FooterHeight = 48;

        private static ResultForm _current;

        private readonly Color _cardBack = Color.FromArgb(247, 247, 250);
        private readonly Color _border = Color.FromArgb(216, 216, 224);
        private readonly Color _muted = Color.FromArgb(110, 110, 122);
        private readonly float _scale;

        private ResultForm(IList<string> results, Icon icon, float scale)
        {
            _scale = scale;

            Text = results.Count > 1
                ? results.Count + " QR codes détectés"
                : "QR code détecté";

            if (icon != null) Icon = icon;

            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;

            // Le panneau rempli est ajouté en premier : WinForms place les
            // contrôles ancrés du dernier au premier, donc celui-ci reçoit
            // la place restante une fois le pied de page posé.
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(S(Gutter), S(Gutter), S(Gutter), 0)
            };

            var offset = 0;
            foreach (var result in results)
            {
                var card = BuildCard(result);
                card.Location = new Point(0, offset);
                content.Controls.Add(card);
                offset += card.Height + S(12);
            }

            var contentHeight = Math.Min(offset + S(Gutter), S(MaxContentHeight));

            Controls.Add(content);
            Controls.Add(BuildFooter());

            ClientSize = new Size(S(CardWidth + Gutter * 2) + S(6), contentHeight + S(FooterHeight));

            KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
        }

        public static void ShowResults(IList<string> results, Icon icon)
        {
            if (results == null || results.Count == 0) return;

            if (_current != null && !_current.IsDisposed)
                _current.Close();

            // La fenêtre s'ouvre près du curseur : on se cale sur le DPI de cet écran-là.
            var cursor = Cursor.Position;

            var form = new ResultForm(results, icon, Dpi.ScaleForPoint(cursor));
            form.FormClosed += (sender, e) => { if (ReferenceEquals(_current, form)) _current = null; };
            form.PlaceNear(cursor);

            _current = form;
            form.Show();
            form.Activate();
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        private Control BuildCard(string text)
        {
            var cardWidth = S(CardWidth);

            var box = new TextBox
            {
                Text = text,
                ReadOnly = true,
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _cardBack,
                Width = cardWidth,
                Location = new Point(0, 0),
                Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point)
            };
            box.Height = MeasureHeight(text, box.Font, cardWidth - S(12));

            var actions = new FlowLayoutPanel
            {
                Location = new Point(0, box.Height + S(8)),
                Width = cardWidth,
                Height = S(ActionsHeight),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            // Les boutons se dimensionnent sur leur texte, donc déjà au bon DPI.
            var copy = new Button
            {
                Text = "Copier",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(S(8), S(1), S(8), S(1))
            };
            copy.Click += (sender, e) =>
            {
                if (Util.TrySetClipboardText(text)) FlashCopied(copy);
            };
            actions.Controls.Add(copy);

            Uri uri;
            if (Util.TryGetLaunchableUri(text, out uri))
            {
                var target = uri;
                var open = new Button
                {
                    Text = target.Scheme == Uri.UriSchemeMailto ? "Écrire un mail" : "Ouvrir le lien",
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(S(8), S(1), S(8), S(1))
                };
                open.Click += (sender, e) =>
                {
                    try
                    {
                        Process.Start(target.AbsoluteUri);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Impossible d'ouvrir ce lien :" + Environment.NewLine + ex.Message,
                            Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    Close();
                };
                actions.Controls.Add(open);
            }
            else
            {
                actions.Controls.Add(new Label
                {
                    Text = text.Length + " caractères",
                    AutoSize = true,
                    ForeColor = _muted,
                    Padding = new Padding(S(8), S(7), 0, 0)
                });
            }

            var card = new Panel
            {
                Width = cardWidth,
                Height = box.Height + S(8) + actions.Height
            };
            card.Controls.Add(box);
            card.Controls.Add(actions);

            return card;
        }

        private Control BuildFooter()
        {
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = S(FooterHeight)
            };

            var hint = new Label
            {
                Text = "Échap pour fermer",
                AutoSize = true,
                ForeColor = _muted,
                Location = new Point(S(Gutter), S(16))
            };

            var close = new Button
            {
                Text = "Fermer",
                Size = new Size(S(88), S(28)),
                Location = new Point(S(CardWidth + Gutter - 84), S(10)),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            close.Click += (sender, e) => Close();

            footer.Controls.Add(hint);
            footer.Controls.Add(close);
            footer.Paint += (sender, e) =>
            {
                using (var pen = new Pen(_border))
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            AcceptButton = close;
            CancelButton = close;

            return footer;
        }

        private void PlaceNear(Point cursor)
        {
            var area = Screen.FromPoint(cursor).WorkingArea;

            var x = Math.Min(Math.Max(cursor.X - Width / 2, area.Left + 12), Math.Max(area.Left, area.Right - Width - 12));
            var y = Math.Min(Math.Max(cursor.Y + 24, area.Top + 12), Math.Max(area.Top, area.Bottom - Height - 12));

            Location = new Point(x, y);
        }

        private int MeasureHeight(string text, Font font, int width)
        {
            var measured = TextRenderer.MeasureText(
                text, font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            return Math.Min(Math.Max(measured.Height + S(10), S(MinBoxHeight)), S(MaxBoxHeight));
        }

        private static void FlashCopied(Button button)
        {
            var original = button.Text;
            button.Text = "Copié !";
            button.Enabled = false;

            var timer = new Timer { Interval = 1100 };
            timer.Tick += (sender, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (button.IsDisposed) return;
                button.Text = original;
                button.Enabled = true;
            };
            timer.Start();
        }
    }
}
