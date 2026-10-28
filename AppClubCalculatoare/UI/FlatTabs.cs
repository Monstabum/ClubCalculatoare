using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Bară de tab-uri plată, în stilul Linear: text discret, iar tab-ul
    /// activ este marcat cu o linie de 2px în culoarea de accent — fără
    /// fundal plin, fără chenar de sistem.
    ///
    /// Înlocuiește <see cref="TabControl"/>-ul nativ, care își desenează
    /// banda de tab-uri cu culorile temei Windows (bandă deschisă, imposibil
    /// de integrat în tema închisă a aplicației).
    ///
    /// Utilizare: se adaugă pe părinte înaintea panoului de conținut
    /// (Dock.Top), apoi <see cref="Add"/> pentru fiecare titlu și abonare la
    /// <see cref="SelectedIndexChanged"/> pentru a comuta paginile.
    /// </summary>
    internal class FlatTabs : Panel
    {
        private readonly List<string> _titluri = new List<string>();
        private readonly List<Rectangle> _zone = new List<Rectangle>();
        private int _activ;
        private int _hover = -1;

        private readonly Font _fNormal = Design.FontSubtitle;
        private readonly Font _fActiv = Design.SemiBold(9f);

        /// <summary>Se declanșează când tab-ul activ se schimbă.</summary>
        public event EventHandler SelectedIndexChanged;

        public FlatTabs()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Design.Surface;
            Dock = DockStyle.Top;
            Height = 34;
            Cursor = Cursors.Hand;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _fNormal.Dispose(); _fActiv.Dispose(); }
            base.Dispose(disposing);
        }

        /// <summary>Indexul tab-ului activ (0 dacă nu s-a selectat nimic explicit).</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _activ;
            set
            {
                if (value < 0 || value >= _titluri.Count || value == _activ) return;
                _activ = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Adaugă un tab (titlul este afișat ca atare).</summary>
        public void Add(string titlu)
        {
            _titluri.Add(titlu ?? "");
            RecalculeazaZonele();
            Invalidate();
        }

        /// <summary>Lățimea naturală a fiecărui tab (text + padding orizontal).</summary>
        private void RecalculeazaZonele()
        {
            _zone.Clear();
            int x = Design.Sp3;                 // mic decupaj față de marginea cardului
            using (var g = CreateGraphics())
            {
                for (int i = 0; i < _titluri.Count; i++)
                {
                    int w = TextRenderer.MeasureText(g, _titluri[i], _fNormal,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + Design.Sp6;
                    if (w < 96) w = 96;
                    _zone.Add(new Rectangle(x, 0, w, Height));
                    x += w;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculeazaZonele();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int nou = IndexLa(e.Location);
            if (nou != _hover) { _hover = nou; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover != -1) { _hover = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int i = IndexLa(e.Location);
            if (i >= 0) SelectedIndex = i;
            base.OnMouseDown(e);
        }

        private int IndexLa(Point p)
        {
            for (int i = 0; i < _zone.Count; i++)
                if (_zone[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            using (var brush = new SolidBrush(Design.Surface))
                g.FillRectangle(brush, ClientRectangle);

            // Separator fin pe toată lățimea, sub rândul de tab-uri.
            Design.DrawSeparator(g, Height - 1, Width, Design.Divider);

            for (int i = 0; i < _titluri.Count && i < _zone.Count; i++)
            {
                var zona = _zone[i];
                bool activ = i == _activ;
                bool hover = i == _hover;

                TextRenderer.DrawText(g, _titluri[i], activ ? _fActiv : _fNormal, zona,
                    (activ || hover) ? Design.Text : Design.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                if (activ)
                    using (var brush = new SolidBrush(Design.Accent))
                        g.FillRectangle(brush, zona.X + Design.Sp3, Height - 2,
                            Math.Max(4, zona.Width - Design.Sp6), 2);
            }
        }
    }
}
