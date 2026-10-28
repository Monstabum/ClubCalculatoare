using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Etichetă cu fundal rotunjit (badge / chip), desenată integral în
    /// OnPaint cu antialiasing. Înlocuiește tiparul vechi
    /// „Label + ApplyRounded(Region)": Region-ul decupează cu o mască de
    /// 1 bit (fără culoare parțială pe muchii), deci colțurile ieșeau
    /// „dințate" — mai ales după scalarea DPI.
    /// </summary>
    internal class RoundLabel : Label
    {
        [DefaultValue(8)]
        public int CornerRadius { get; set; } = 8;

        public RoundLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Fundalul din spatele colțurilor = primul părinte opac.
            g.Clear(Design.ResolveOpaqueBackground(this, Design.Surface));

            var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color umplere = BackColor;
            if (umplere.A > 0)
            {
                using (var path = Design.RoundedPath(rect, CornerRadius))
                using (var br = new SolidBrush(Color.FromArgb(255, umplere)))
                    g.FillPath(br, path);
            }

            if (!string.IsNullOrEmpty(Text))
            {
                TextFormatFlags flags = TextFormatFlags.NoPrefix;
                switch (TextAlign)
                {
                    case ContentAlignment.TopLeft:      flags |= TextFormatFlags.Top | TextFormatFlags.Left; break;
                    case ContentAlignment.TopCenter:    flags |= TextFormatFlags.Top | TextFormatFlags.HorizontalCenter; break;
                    case ContentAlignment.TopRight:     flags |= TextFormatFlags.Top | TextFormatFlags.Right; break;
                    case ContentAlignment.MiddleLeft:   flags |= TextFormatFlags.VerticalCenter | TextFormatFlags.Left; break;
                    case ContentAlignment.MiddleRight:  flags |= TextFormatFlags.VerticalCenter | TextFormatFlags.Right; break;
                    case ContentAlignment.BottomLeft:   flags |= TextFormatFlags.Bottom | TextFormatFlags.Left; break;
                    case ContentAlignment.BottomCenter: flags |= TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter; break;
                    case ContentAlignment.BottomRight:  flags |= TextFormatFlags.Bottom | TextFormatFlags.Right; break;
                    default:                            flags |= TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter; break;
                }
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor, flags);
            }
        }
    }
}
