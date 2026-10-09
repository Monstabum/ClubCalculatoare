using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace AppClubCalculatoare.UI
{
    internal enum ButtonVariant { Primary, Secondary, Ghost, Danger, Success }

    internal class ModernButton : Button
    {
        public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;
        public int CornerRadius { get; set; } = Theme.Radius;
        public bool ShowShadow { get; set; } = false;
        private bool _hover;
        private bool _pressed;

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);  // necesar pentru fundaluri custom
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            // Nu lăsăm stratul standard să pinteze fundalul la hover/apăsare — desenăm noi totul.
            FlatAppearance.MouseOverBackColor = Color.Empty;
            FlatAppearance.MouseDownBackColor = Color.Empty;
            BackColor = Theme.Surface;                          // dens implicit, NU Transparent
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
            Height = MaterialTheme.Scale(36);
            Padding = new Padding(12, 0, 12, 0);
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Theme.Rounded(this, CornerRadius); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.Rounded(this, CornerRadius); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Nu golitim niciodată cu transparent — luăm primul culoare dens pe lanțul de părinți.
            Color bg = Theme.ResolveOpaqueBackground(this, Theme.Surface);
            g.Clear(bg);

            Color back, fore, border;
            ResolveColors(bg, out back, out fore, out border);

            if (!Enabled)
            {
                back = MaterialTheme.Surface;
                fore = MaterialTheme.TextSecondary;
                border = MaterialTheme.Divider;
            }

            var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (var path = MaterialTheme.RoundedPath(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(back))           // back e ÎNTOTDEAUNA dens
                    g.FillPath(brush, path);
                if (border.A > 16 && border.ToArgb() != back.ToArgb())
                    using (var pen = new Pen(border, 1f))
                        g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, Text ?? string.Empty, Font, ClientRectangle, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // bg — fundal dens rezolvat; toate stările „transparente” sunt înlocuite cu bg / Blend(bg, …).
        private void ResolveColors(Color bg, out Color back, out Color fore, out Color border)
        {
            switch (Variant)
            {
                case ButtonVariant.Primary:
                    back = _pressed ? Theme.AccentHover : (_hover ? Theme.AccentHover : Theme.Accent);
                    fore = Color.White; border = back; break;

                case ButtonVariant.Secondary:
                    back = _hover ? MaterialTheme.SurfaceAlt : MaterialTheme.Surface;
                    fore = _hover ? MaterialTheme.Accent : MaterialTheme.TextPrimary;
                    border = MaterialTheme.Divider; break;

                case ButtonVariant.Ghost:
                    back = _hover ? MaterialTheme.SurfaceAlt : MaterialTheme.Surface;
                    fore = _hover ? MaterialTheme.TextPrimary : MaterialTheme.TextSecondary;
                    border = Color.Transparent; break;

                case ButtonVariant.Danger:
                    back = _pressed ? Color.FromArgb(0xB0, 0x35, 0x35)
                                    : (_hover ? Color.FromArgb(0xC0, 0x3A, 0x3A) : Theme.Danger);
                    fore = Color.White; border = back; break;

                case ButtonVariant.Success:
                    back = _pressed ? Color.FromArgb(0x07, 0x85, 0x52)
                                    : (_hover ? Color.FromArgb(0x08, 0x91, 0x5A) : Theme.Success);
                    fore = Color.White; border = back; break;

                default:
                    back = Theme.Accent; fore = Color.White; border = back; break;
            }
        }
    }
}