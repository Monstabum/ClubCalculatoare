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
            using (var path = MaterialTheme.RoundedPath(rect, CornerRadius))
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
    }
}
