using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Element de navigare din sidebar, în stilul „Linear": plat, fără
    /// chenar și fără umbră. Starea activă este marcată de o bară verticală
    /// de 2px în culoarea de accent și de o suprafață cyan translucidă;
    /// hover-ul ridică discret suprafața.
    /// </summary>
    internal class NavButton : Button
    {
        [DefaultValue("")]
        public string Icon { get; set; } = "";
        [DefaultValue(null)]
        public string BadgeText { get; set; } = null;

        private bool _hover;
        private bool _active;
        private bool _tooltipAtasat;

        // Poziții fixe din specificația de design.
        private const int IconX = 14;
        private const int TextX = 44;

        // Fonturi cache-uite (butoanele de meniu se repictează la hover/activare;
        // proprietățile Design.Font* creează un Font nou la fiecare accesare).
        private readonly Font _fNav      = Design.FontNav;
        private readonly Font _fNavActiv = Design.FontNavActive;
        private readonly Font _fBadge    = Design.FontSmallBold;

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _fNav.Dispose(); _fNavActiv.Dispose(); _fBadge.Dispose(); }
            base.Dispose(disposing);
        }

        [DefaultValue(false)]
        public bool Active
        {
            get => _active;
            set { _active = value; Invalidate(); }
        }

        public override string Text
        {
            get { return base.Text; }
            set { base.Text = value; AsiguraTooltip(); }
        }

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Height = Design.NavItemH;
            Font = Design.FontNav;
            Cursor = Cursors.Hand;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(0);
            ParentChanged += (s, e) => AsiguraTooltip();
            // Retries la creearea handle-ului: in Designer, Text este setat
            // inainte ca butonul sa fie legat de arborele formei, deci nici
            // ParentChanged singur nu garanteaza gasirea formei.
            HandleCreated += (s, e) => AsiguraTooltip();
        }

        private void AsiguraTooltip()
        {
            if (_tooltipAtasat || string.IsNullOrEmpty(Text)) return;
            // AtasareTooltip intoarce false daca controlul nu este inca
            // atasat la o forma (FindForm() == null) — cazul in care
            // Designerul seteaza Text inainte de a adauga butonul in arbore.
            // Nu marcam _tooltipAtasat in acest caz: ParentChanged va
            // reincearca si tooltipul va aparea efectiv.
            _tooltipAtasat = MaterialUIHelper.AtasareTooltip(this, Text, Text);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true; Invalidate(); base.OnMouseEnter(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Fundalul din spatele controlului (sidebar-ul este opac).
            g.Clear(Design.ResolveOpaqueBackground(this, Design.Bg));

            Color fore = _active ? Design.Text
                       : (_hover ? Design.Text : Design.TextMuted);

            var rect = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));

            // Suprafață: transparentă (idle) → gri translucid (hover) →
            // cyan translucid (activ). Fără chenar.
            Color suprafata = _active ? Design.Alpha(Design.Accent, 30)
                            : (_hover ? Design.Alpha(Color.White, 6) : Color.Transparent);
            if (suprafata.A > 0)
                using (var brush = new SolidBrush(suprafata))
                    g.FillRectangle(brush, rect);

            // Bară verticală de 2px în culoarea de accent pe elementul activ.
            if (_active)
                using (var brush = new SolidBrush(Design.Accent))
                    g.FillRectangle(brush, 0, 6, 2, Math.Max(4, Height - 12));

            // Pictogramă (glif Segoe Fluent Icons, 12pt).
            if (!string.IsNullOrEmpty(Icon))
                MaterialTheme.DrawGlyph(g, Icon, new Point(IconX, (Height - 20) / 2), 12f, fore);

            // Text.
            int x = string.IsNullOrEmpty(Icon) ? Design.Sp4 : TextX;
            var textRect = new Rectangle(x, 0, Math.Max(10, Width - x - 12), Height);
            TextRenderer.DrawText(g, Text ?? string.Empty,
                _active ? _fNavActiv : _fNav,
                textRect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);

            // Badge numeric (opțional) — pastilă discretă, nu accent colorat.
            if (!string.IsNullOrEmpty(BadgeText))
            {
                int bW = TextRenderer.MeasureText(BadgeText, _fBadge).Width + Design.Sp3;
                int bH = 18;
                int bX = Width - bW - Design.Sp3;
                int bY = (Height - bH) / 2;

                using (var path = Design.RoundedPath(new Rectangle(bX, bY, bW, bH), 9))
                using (var brush = new SolidBrush(Design.SurfaceRaised))
                    g.FillPath(brush, path);

                TextRenderer.DrawText(g, BadgeText, _fBadge,
                    new Rectangle(bX, bY, bW, bH), Design.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
