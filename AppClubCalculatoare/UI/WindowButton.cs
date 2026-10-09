using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    internal enum WindowButtonKind { Minimize, Maximize, Restore, Close }

    /// <summary>
    /// Buton personalizat pentru chrome-ul ferestrei (min / max / restore / close).
    /// Desenare custom (fără font-emoji): linii și dreptunghiuri vectoriale, stări
    /// hover/pressed. Butonul „Închide\" are roșu de accent la hover, ca pe Win11.
    /// Stil „Neon Lobby\" — culori preluate din <see cref="MaterialTheme"/>.
    /// </summary>
    internal class WindowButton : Control
    {
        public WindowButtonKind Kind { get; set; } = WindowButtonKind.Close;

        private bool _hover;
        private bool _pressed;

        public WindowButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(MaterialTheme.Scale(46), MaterialTheme.Scale(32));
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = MaterialTheme.ResolveOpaqueBackground(this, MaterialTheme.Surface);
            g.Clear(bg);

            if (_hover || _pressed)
            {
                Color fill = (Kind == WindowButtonKind.Close)
                    ? (_pressed ? MaterialTheme.Darker(MaterialTheme.Danger, 0.18f) : MaterialTheme.Danger)
                    : (_pressed ? MaterialTheme.Darker(MaterialTheme.SurfaceAlt, 0.10f) : MaterialTheme.SurfaceAlt);
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = MaterialTheme.RoundedPath(rect, 5))
                using (var b = new SolidBrush(fill))
                    g.FillPath(b, path);
            }

            Color glyph = _hover ? Color.White : MaterialTheme.TextSecondary;
            int cx = Width / 2, cy = Height / 2, s = 5;
            using (var pen = new Pen(glyph, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                switch (Kind)
                {
                    case WindowButtonKind.Minimize:
                        g.DrawLine(pen, cx - s, cy, cx + s, cy);
                        break;
                    case WindowButtonKind.Maximize:
                        g.DrawRectangle(pen, cx - s, cy - s, s * 2, s * 2);
                        break;
                    case WindowButtonKind.Restore:
                        g.DrawRectangle(pen, cx - 1, cy - s, s + 1, s + 1);
                        g.DrawRectangle(pen, cx - s, cy - 1, s + 1, s + 1);
                        break;
                    case WindowButtonKind.Close:
                        g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s);
                        g.DrawLine(pen, cx + s, cy - s, cx - s, cy + s);
                        break;
                }
            }
        }
    }
}
