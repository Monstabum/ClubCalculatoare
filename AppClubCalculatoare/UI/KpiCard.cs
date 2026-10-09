using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Card KPI: valoare numerică mare, etichetă mică, subtitlu,
    /// trend opțional și bară de accent colorată în stânga.
    /// </summary>
    internal class KpiCard : Panel
    {
        private string _title = "";
        private string _value = "-";
        private string _unit = "";
        private string _subtitle = "";
        private string _trendText = "";
        private Color _trendColor = Theme.TextMuted;
        private Color _accent = Theme.Accent;
        private string _icon = "";

        public string Title    { get => _title;    set { _title = value; Invalidate(); } }
        public string Value    { get => _value;    set { _value = value; Invalidate(); } }
        public string Unit     { get => _unit;     set { _unit = value; Invalidate(); } }
        public string Subtitle { get => _subtitle; set { _subtitle = value; Invalidate(); } }
        public string Icon     { get => _icon;     set { _icon = value; Invalidate(); } }

        public string TrendText
        {
            get => _trendText;
            set { _trendText = value; Invalidate(); }
        }

        public Color TrendColor
        {
            get => _trendColor;
            set { _trendColor = value; Invalidate(); }
        }

        public Color Accent
        {
            get => _accent;
            set { _accent = value; Invalidate(); }
        }

        public KpiCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Height = MaterialTheme.Scale(110);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Theme.Rounded(this, Theme.Radius);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Rounded(this, Theme.Radius);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);

            // Border subtil
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            // Bară de accent (vertical, stânga)
            using (var brush = new SolidBrush(_accent))
                g.FillRectangle(brush, 0, 0, 4, Height);

            // Iconiță + titlu
            int y = 14;
            if (!string.IsNullOrEmpty(_icon))
            {
                using (var f = new Font("Segoe UI Emoji", 10f))
                    TextRenderer.DrawText(g, _icon, f,
                        new Point(18, y), Theme.TextMuted);
            }
            TextRenderer.DrawText(g, _title, Theme.SmallBold,
                new Rectangle(42, y, Width - 58, 18), Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);

            // Valoare mare
            y = 36;
            var szVal = TextRenderer.MeasureText(_value, Theme.KpiValue);
            TextRenderer.DrawText(g, _value, Theme.KpiValue,
                new Point(18, y), Theme.Text);

            // Unitate
            if (!string.IsNullOrEmpty(_unit))
            {
                TextRenderer.DrawText(g, _unit, Theme.KpiUnit,
                    new Point(18 + szVal.Width + 6, y + 12), Theme.TextMuted);
            }

            // Subtitlu
            y = 72;
            TextRenderer.DrawText(g, _subtitle, Theme.Caption,
                new Rectangle(18, y, Width - 30, 16), Theme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);

            // Trend
            if (!string.IsNullOrEmpty(_trendText))
            {
                y = 88;
                TextRenderer.DrawText(g, _trendText, Theme.Caption,
                    new Rectangle(18, y, Width - 30, 16), _trendColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);
            }
        }
    }
}
