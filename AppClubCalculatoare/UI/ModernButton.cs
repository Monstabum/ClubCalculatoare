using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    internal enum ButtonVariant { Primary, Secondary, Ghost, Danger, Success }

    /// <summary>
    /// Buton plat, fără umbră, fără gradient și fără glow. Aceeași limbă
    /// vizuală ca <see cref="MaterialButton"/>, folosită de componentele
    /// construite direct în cod (dashboard, harta sălii).
    /// </summary>
    internal class ModernButton : Button
    {
        [DefaultValue(ButtonVariant.Primary)]
        public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;
        [DefaultValue(Design.RadiusSmall)]
        public int CornerRadius { get; set; } = Design.RadiusSmall;
        private bool _hover;
        private bool _pressed;

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);  // necesar pentru fundaluri custom
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            // Nu lăsăm stratul standard să pinteze fundalul la hover/apăsare — desenăm noi totul.
            FlatAppearance.MouseOverBackColor = Color.Empty;
            FlatAppearance.MouseDownBackColor = Color.Empty;
            BackColor = Design.Surface;                          // dens implicit, NU Transparent
            Font = Design.FontButton;
            Cursor = Cursors.Hand;
            Height = Design.ButtonH;
            Padding = new Padding(Design.Sp3, 0, Design.Sp3, 0);
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Nu golitim niciodată cu transparent — luăm prima culoare densă pe lanțul de părinți.
            Color bg = Design.ResolveOpaqueBackground(this, Design.Surface);
            g.Clear(bg);

            Color back, fore, border, overlay;
            ResolveColors(out back, out fore, out border, out overlay);

            if (!Enabled)
            {
                back = Design.Surface;
                fore = Design.TextDim;
                border = Design.Border;
                overlay = Color.Transparent;
            }

            var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));

            using (var path = Design.RoundedPath(rect, CornerRadius))
            {
                if (back.A > 0)
                    using (var brush = new SolidBrush(back))
                        g.FillPath(brush, path);

                if (overlay.A > 0)
                    using (var brush = new SolidBrush(overlay))
                        g.FillPath(brush, path);

                if (border.A > 16)
                    using (var pen = new Pen(border, 1f))
                        g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, Text ?? string.Empty, Font, ClientRectangle, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void ResolveColors(out Color back, out Color fore,
            out Color border, out Color overlay)
        {
            bool activ = _hover || _pressed;
            overlay = Color.Transparent;

            switch (Variant)
            {
                case ButtonVariant.Primary:
                    back = activ ? Design.AccentHover : Design.Accent;
                    fore = Design.AccentText;
                    border = Color.Transparent;
                    return;

                case ButtonVariant.Secondary:
                    back = Design.SurfaceRaised;
                    fore = Design.Text;
                    border = Design.Border;
                    overlay = _hover ? Design.Alpha(Design.Text, 10) : Color.Transparent;
                    return;

                case ButtonVariant.Ghost:
                    back = Color.Transparent;
                    fore = _hover ? Design.Text : Design.TextMuted;
                    border = Color.Transparent;
                    overlay = _hover ? Design.Alpha(Color.White, 6) : Color.Transparent;
                    return;

                case ButtonVariant.Danger:
                    back = activ ? Design.DangerBright : Design.Danger;
                    fore = Color.White;
                    border = Color.Transparent;
                    return;

                case ButtonVariant.Success:
                    back = activ ? Design.SuccessBright : Design.Success;
                    fore = Color.White;
                    border = Color.Transparent;
                    return;

                default:
                    back = Design.Accent;
                    fore = Design.AccentText;
                    border = Color.Transparent;
                    return;
            }
        }
    }
}
