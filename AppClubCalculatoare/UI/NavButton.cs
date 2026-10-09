using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Buton de navigare pentru sidebar. Afișează o pictogramă emoji,
    /// un text și, opțional, un badge numeric. Se evidențiază când e activ.
    /// </summary>
    internal class NavButton : Button
    {
        public string Icon { get; set; } = "";
        public string BadgeText { get; set; } = null;

        private bool _hover;
        private bool _active;

        public bool Active
        {
            get => _active;
            set { _active = value; Invalidate(); }
        }

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Height = 42;
            Font = Theme.NavFont;
            Cursor = Cursors.Hand;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(0);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true; Invalidate(); base.OnMouseEnter(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false; Invalidate(); base.OnMouseLeave(e);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Theme.Rounded(this, Theme.RadiusSmall);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Rounded(this, Theme.RadiusSmall);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Parent?.BackColor ?? Theme.Sidebar);

            // Pe Sidebar închis (#131A29): activ = pastilă violet cu bară
            // accent cyan în stânga; hover = suprafață ridicată; idle = transparent.
            Color back = _active ? Theme.Accent
                       : (_hover ? MaterialTheme.SurfaceAlt : Color.Transparent);
            Color fore = _active ? Color.White
                       : (_hover ? MaterialTheme.TextPrimary : MaterialTheme.TextSecondary);

            if (back != Color.Transparent)
            {
                var rect = new Rectangle(2, 1, Math.Max(1, Width - 5), Math.Max(1, Height - 3));
                using (var path = RoundedPath(rect, Theme.RadiusSmall))
                {
                    using (var brush = new SolidBrush(back))
                        g.FillPath(brush, path);
                    if (_active)
                        using (var pen = new Pen(MaterialTheme.Lighter(Theme.Accent, 0.35f), 1f))
                            g.DrawPath(pen, path);
                }

                // Bară accent cyan în stânga pentru elementul activ.
                if (_active)
                {
                    using (var brush = new SolidBrush(MaterialTheme.Accent))
                    {
                        var stripe = new Rectangle(4, Height / 2 - 11, 3, 22);
                        using (var path = RoundedPath(stripe, 2))
                            g.FillPath(brush, path);
                    }
                }
            }

            // Pictogramă
            int x = 16;
            if (!string.IsNullOrEmpty(Icon))
            {
                using (var f = new Font("Segoe UI Emoji", 11f))
                    TextRenderer.DrawText(g, Icon, f,
                        new Point(x, (Height - 20) / 2),
                        _active ? Color.White : fore, TextFormatFlags.Default);
                x += 30;
            }

            // Text
            var textRect = new Rectangle(x, 0, Width - x - 60, Height);
            TextRenderer.DrawText(g, Text,
                _active ? Theme.NavFontActive : Theme.NavFont,
                textRect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);

            // Badge
            if (!string.IsNullOrEmpty(BadgeText))
            {
                var fBadge = Theme.SmallBold;
                int bW = TextRenderer.MeasureText(BadgeText, fBadge).Width + 14;
                int bH = 20;
                int bX = Width - bW - 12;
                int bY = (Height - bH) / 2;

                using (var brush = new SolidBrush(Theme.Warning))
                using (var path = RoundedPath(new Rectangle(bX, bY, bW, bH), 10))
                    g.FillPath(brush, path);

                TextRenderer.DrawText(g, BadgeText, fBadge,
                    new Rectangle(bX, bY, bW, bH), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            int rad = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
            var path = new GraphicsPath();
            path.StartFigure();
            path.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            path.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            path.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
