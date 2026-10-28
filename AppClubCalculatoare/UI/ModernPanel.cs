using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Panel plat cu colțuri rotunjite și contur subtil: suprafață
    /// <see cref="Design.Surface"/> + 1px <see cref="Design.Border"/>.
    /// Fără gradient, fără umbră.
    /// </summary>
    internal class ModernPanel : Panel
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = Design.Radius;
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowBorder { get; set; } = true;
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Design.Border;
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SurfaceColor { get; set; } = Design.Surface;

        public ModernPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = SurfaceColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Design.ResolveOpaqueBackground(this, Design.Bg));

            var rect = new Rectangle(0, 0, Math.Max(2, Width - 1), Math.Max(2, Height - 1));
            using (var path = Design.RoundedPath(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(SurfaceColor))
                    g.FillPath(brush, path);

                if (ShowBorder)
                    using (var pen = new Pen(BorderColor, 1f))
                        g.DrawPath(pen, path);
            }
        }
    }
}
