using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Panel cu colțuri rotunjite, border subtil și opțional un titlu.
    /// Folosit pentru cardurile din dashboard și rapoarte.
    /// </summary>
    internal class ModernPanel : Panel
    {
        public int CornerRadius { get; set; } = Theme.Radius;
        public bool ShowBorder { get; set; } = true;
        public Color BorderColor { get; set; } = Theme.Border;
        public Color SurfaceColor { get; set; } = Theme.Surface;

        public ModernPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = SurfaceColor;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Theme.Rounded(this, CornerRadius);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Rounded(this, CornerRadius);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Theme.Background);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedPath(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(SurfaceColor))
                    g.FillPath(brush, path);

                if (ShowBorder)
                {
                    using (var pen = new Pen(BorderColor, 1f))
                        g.DrawPath(pen, path);
                }
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
