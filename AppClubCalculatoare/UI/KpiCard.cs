using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Card KPI în stilul „metric header" folosit de Stripe / Linear:
    /// o etichetă uppercase discretă, valoarea mare ca element dominant,
    /// iar jos un subtitlu și un trend aliniat la dreapta.
    ///
    /// Cardul NU desenează nici chenar, nici umbră, nici bară de accent:
    /// separatoarele de 1px vin din containerul părinte (TableLayoutPanel
    /// cu fundal <see cref="Design.Border"/>), iar ierarhia este dată
    /// exclusiv de suprafață și tipografie.
    /// </summary>
    internal class KpiCard : Panel
    {
        private string _title = "";
        private string _value = "-";
        private string _unit = "";
        private string _subtitle = "";
        private string _trendText = "";
        private Color _trendColor = Design.TextMuted;

        /// <summary>
        /// Păstrat pentru compatibilitate cu apelurile existente. Nu are
        /// efect vizual: noul card nu folosește culoare de accent.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color Accent { get; set; } = Design.Accent;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Value
        {
            get => _value;
            set { _value = value ?? ""; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Unit
        {
            get => _unit;
            set { _unit = value ?? ""; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value ?? ""; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TrendText
        {
            get => _trendText;
            set { _trendText = value ?? ""; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color TrendColor
        {
            get => _trendColor;
            set { _trendColor = value; Invalidate(); }
        }

        // Fonturi cache-uite pe instanță: cardurile se repictează des (refresh
        // de date, resize), iar proprietățile Design.Font* creează un Font nou
        // la fiecare accesare — fără cache s-ar pierde handle-uri GDI.
        private readonly Font _fOverline = Design.FontOverline;
        private readonly Font _fValue    = Design.FontKpiValue;
        private readonly Font _fUnit     = Design.FontKpiUnit;
        private readonly Font _fCaption  = Design.FontSmall;
        private readonly Font _fTrend    = Design.FontSmallBold;

        // Padding intern (grila de 4px): 16 stânga/dreapta, 14 sus/jos.
        private const int PadL = Design.Sp4;
        private const int PadT = 14;
        private const int PadR = Design.Sp4;
        private const int PadB = 14;

        public KpiCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Design.Surface;
            Height = 120;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fOverline.Dispose();
                _fValue.Dispose();
                _fUnit.Dispose();
                _fCaption.Dispose();
                _fTrend.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Fără chenar propriu: umplem doar suprafața cardului. Separatoarele
            // de 1px sunt desenate de containerul părinte.
            using (var brush = new SolidBrush(Design.Surface))
                g.FillRectangle(brush, ClientRectangle);

            int latime = Width - PadL - PadR;
            if (latime <= 0 || Height <= 0) return;

            // ── 1. Etichetă uppercase, tracking larg, TextDim ──────
            int y = PadT;
            int hOverline = DrawTrackingText(g, _title.ToUpperInvariant(), _fOverline,
                Design.TextDim, PadL, y, latime, 1);
            y += hOverline + Design.Sp2;

            // ── 2. Valoare mare (Text) + unitate (TextDim) ─────────
            var szVal = TextRenderer.MeasureText(g, _value, _fValue,
                new Size(int.MaxValue, int.MaxValue), FlagsNoPadding);
            int hVal = Math.Max(_fValue.Height, szVal.Height);

            TextRenderer.DrawText(g, _value, _fValue,
                new Rectangle(PadL, y, Math.Max(10, latime), hVal), Design.Text,
                TextFormatFlags.Left | TextFormatFlags.Top |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis);

            if (!string.IsNullOrEmpty(_unit))
            {
                var szUnit = TextRenderer.MeasureText(g, _unit, _fUnit,
                    new Size(int.MaxValue, int.MaxValue), FlagsNoPadding);
                TextRenderer.DrawText(g, _unit, _fUnit,
                    new Point(PadL + szVal.Width + Design.Sp1,
                              y + Math.Max(0, hVal - szUnit.Height)),
                    Design.TextDim,
                    TextFormatFlags.Left | TextFormatFlags.Top |
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }

            // ── 3. Rândul de jos: subtitlu (stânga) + trend (dreapta) ──
            int hBottom = Math.Max(_fCaption.Height, _fTrend.Height);
            int bottomY = Math.Max(y + hVal + Design.Sp1, Height - PadB - hBottom);

            int trendW = 0;
            if (!string.IsNullOrEmpty(_trendText))
            {
                trendW = TextRenderer.MeasureText(g, _trendText, _fTrend,
                    new Size(int.MaxValue, int.MaxValue), FlagsNoPadding).Width;

                TextRenderer.DrawText(g, _trendText, _fTrend,
                    new Rectangle(Width - PadR - trendW, bottomY, trendW, hBottom),
                    _trendColor,
                    TextFormatFlags.Right | TextFormatFlags.Top |
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }

            int subW = latime - (trendW > 0 ? trendW + Design.Sp3 : 0);
            if (subW > 0 && !string.IsNullOrEmpty(_subtitle))
                TextRenderer.DrawText(g, _subtitle, _fCaption,
                    new Rectangle(PadL, bottomY, subW, hBottom), Design.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.Top |
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                    TextFormatFlags.EndEllipsis);
        }

        private const TextFormatFlags FlagsNoPadding =
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        /// <summary>
        /// Desenează text cu spațiere între litere (tracking), folosit pentru
        /// eticheta uppercase a cardului. GDI nu expune letter-spacing, deci
        /// avansăm manual după fiecare caracter. Întoarce înălțimea rândului.
        /// </summary>
        private static int DrawTrackingText(Graphics g, string text, Font font,
            Color color, int x, int y, int maxWidth, int tracking)
        {
            if (string.IsNullOrEmpty(text)) return font.Height;

            int cursor = x;
            int limita = x + maxWidth;
            foreach (char ch in text)
            {
                string s = ch.ToString();
                int w = TextRenderer.MeasureText(g, s, font,
                    new Size(int.MaxValue, int.MaxValue), FlagsNoPadding).Width;
                if (w <= 0) w = 4;

                if (cursor + w > limita) break;   // nu depășim cardul

                TextRenderer.DrawText(g, s, font, new Point(cursor, y), color,
                    TextFormatFlags.Left | TextFormatFlags.Top |
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                cursor += w + tracking;
            }
            return font.Height;
        }
    }
}
