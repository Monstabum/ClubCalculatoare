using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Design-system "Neon Lobby" (dark gaming) pentru AppClubCalculatoare.
    /// Paleta: fundal Lobby Black #0F1420, suprafață petrol #161E2E,
    /// accent principal violet Arcade #7C3AED + cyan HUD #22D3EE.
    /// Rezolvă bug-ul "text invizibil pe butoane" prin OnPaint custom
    /// cu TextRenderer.DrawText; butoanele sunt desenate cu gradient + glow.
    /// </summary>
    internal static class MaterialTheme
    {
        // ── Paleta semantica (dark neon) ──────────────────────────
        public static readonly Color Primary      = Color.FromArgb(0x7C, 0x3A, 0xED); // Arcade violet
        public static readonly Color PrimaryDark  = Color.FromArgb(0x5B, 0x21, 0xB6); // violet adânc
        public static readonly Color PrimaryLight = Color.FromArgb(0xA7, 0x8B, 0xFA); // violet deschis (gradient/glow)
        public static readonly Color Accent       = Color.FromArgb(0x22, 0xD3, 0xEE); // HUD cyan

        public static readonly Color Success = Color.FromArgb(0x16, 0xA3, 0x4A);
        public static readonly Color Danger  = Color.FromArgb(0xE1, 0x1D, 0x48);
        public static readonly Color Warning = Color.FromArgb(0xD9, 0x77, 0x06);
        public static readonly Color Info    = Color.FromArgb(0x08, 0x91, 0xB2);

        public static readonly Color Background = Color.FromArgb(0x0F, 0x14, 0x20); // Lobby black
        public static readonly Color Surface    = Color.FromArgb(0x16, 0x1E, 0x2E); // card petrol
        public static readonly Color SurfaceAlt = Color.FromArgb(0x1C, 0x26, 0x3A); // elevated
        public static readonly Color BottomBar  = Color.FromArgb(0x0B, 0x10, 0x1A); // bara jos, mai închisă
        public static readonly Color SidebarBg  = Color.FromArgb(0x13, 0x1A, 0x29); // meniu lateral

        public static readonly Color TextPrimary   = Color.FromArgb(0xF1, 0xF5, 0xF9); // aproape alb
        public static readonly Color TextSecondary = Color.FromArgb(0x94, 0xA3, 0xB8); // gri albăstriu
        public static readonly Color Divider       = Color.FromArgb(0x23, 0x30, 0x46);

        // ── Tonuri „soft” (migrate din Theme, folosite pe fundal închis) ──
        public static readonly Color TextDim     = Color.FromArgb(0x64, 0x74, 0x8B);
        public static readonly Color BorderStrong = Color.FromArgb(0x33, 0x40, 0x5A);
        public static readonly Color AccentSoft   = Color.FromArgb(0x2A, 0x23, 0x50); // violet soft pe închis
        public static readonly Color WarningSoft  = Color.FromArgb(0x3A, 0x2E, 0x12); // galben soft pe închis

        // ── Dimensiuni ────────────────────────────────────────────
        public const int ButtonRadius = 10;
        public const int CardRadius   = 14;
        public const int TopBarH      = 64;
        public const int BottomBarH   = 64;
        public const int HeaderH      = 76;
        public const int ContentPad   = 20;
        public const int SidebarW     = 230;

        // ── Tipografie (Segoe UI) ─────────────────────────────────
        public const string FontFamily = "Segoe UI";

        public static readonly Font FontTitle    = new Font(FontFamily, 16f, FontStyle.Bold);
        public static readonly Font FontSubtitle = new Font(FontFamily, 11.5f, FontStyle.Bold);
        public static readonly Font FontBody     = new Font(FontFamily, 10f);
        public static readonly Font FontSmall    = new Font(FontFamily, 9f);
        public static readonly Font FontButton   = new Font(FontFamily, 10f, FontStyle.Bold);
        public static readonly Font FontLabel    = new Font(FontFamily, 9f, FontStyle.Bold);

        // ── Alias tipografice (migrate din Theme; valori păstrate identic) ──
        public static readonly Font H1             = new Font(FontFamily, 18f, FontStyle.Bold);
        public static readonly Font H2             = new Font(FontFamily, 14f, FontStyle.Bold);
        public static readonly Font H3             = new Font(FontFamily, 11.5f, FontStyle.Bold);
        public static readonly Font Body           = new Font(FontFamily, 9.5f);
        public static readonly Font BodyBold       = new Font(FontFamily, 9.5f, FontStyle.Bold);
        public static readonly Font Small          = new Font(FontFamily, 8.5f);
        public static readonly Font SmallBold      = new Font(FontFamily, 8.5f, FontStyle.Bold);
        public static readonly Font Caption        = new Font(FontFamily, 8f);
        public static readonly Font CaptionItalic  = new Font(FontFamily, 8f, FontStyle.Italic);
        public static readonly Font NavFont        = new Font(FontFamily, 10f);
        public static readonly Font NavFontActive  = new Font(FontFamily, 10f, FontStyle.Bold);
        public static readonly Font KpiValue       = new Font(FontFamily, 21f, FontStyle.Bold);
        public static readonly Font KpiUnit        = new Font(FontFamily, 9f, FontStyle.Bold);

        // ── Helper-e de culoare ───────────────────────────────────
        // Notă: Blend / Lighter / Darker există deja mai jos (varianta cu overlay alpha
        // pentru butoane). Aici adăugăm doar ajutoarele care lipsesc.

        /// <summary>Adaugă transparență (alpha 0..255) unei culori.</summary>
        public static Color WithAlpha(Color c, int alpha) => Color.FromArgb(alpha, c);

        /// <summary>Familia de glife pentru pictogramele din interfață.</summary>
        public const string GlyphFont = "Segoe Fluent Icons";

        /// <summary>
        /// Desenează un glif Segoe Fluent Icons (fallback „Segoe MDL2 Assets”),
        /// centrat pe înălțime într-un rând la poziția dată.
        /// Render independent de locală (spre deosebire de fontul Emoji).
        /// </summary>
        public static void DrawGlyph(Graphics g, string glyph, Point location, float sizePx, Color color)
        {
            if (g == null || string.IsNullOrEmpty(glyph)) return;
            var font = new Font(GlyphFont, sizePx);
            try
            {
                var rect = new Rectangle(location.X, location.Y, 30, (int)Math.Ceiling(sizePx) + 8);
                TextRenderer.DrawText(g, glyph, font, rect, color,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            finally
            {
                font.Dispose();
            }
        }

        /// <summary>
        /// Scalează o valoare din pixeli logici (96 DPI) la DPI-ul ecranului
        /// curent. Permite controalelor construite în cod (înălțimi/lățimi
        /// hardcodate) să urmeze scalarea Windows pe 125% / 150%.
        /// Fără ecran/GDI rămâne valoarea logică.
        /// </summary>
        public static int Scale(int px)
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    float dpi = g.DpiX;
                    if (dpi <= 0f || Math.Abs(dpi - 96f) < 0.5f) return px;
                    return (int)Math.Round(px * dpi / 96f);
                }
            }
            catch
            {
                return px;   // fără ecran/GDI — rămâne valoarea logică
            }
        }

        // ── Helper: regiune cu colturi rotunjite ──────────────────
        public static void ApplyRounded(Control control, int radius)
        {
            if (control == null || control.Width <= 0 || control.Height <= 0) return;
            int rad = Math.Min(radius, Math.Min(control.Width, control.Height) / 2);
            if (rad <= 0) { control.Region = null; return; }
            using (var path = RoundedPath(new Rectangle(0, 0, control.Width, control.Height), rad))
                control.Region = new Region(path);
        }

        public static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            int rad = Math.Max(0, Math.Min(radius, Math.Min(r.Width, r.Height) / 2));
            var path = new GraphicsPath();
            if (rad <= 0) { path.AddRectangle(r); return path; }
            int d = rad * 2;
            path.StartFigure();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Rezolvare fundal opac + amestec culori (canonic, vezi Theme) ──
        // Implementarea unica a ajutoarelor folosite de butoanele custom
        // (inainte duplicata in Theme). Theme.* redirectioneaza aici.
        public static Color ResolveOpaqueBackground(Control control, Color fallback)
        {
            for (Control c = control; c != null; c = c.Parent)
            {
                Color bc = c.BackColor;
                if (bc.A > 16) return bc;
            }
            Form f = control != null ? control.FindForm() : null;
            if (f != null && f.BackColor.A > 16) return f.BackColor;
            return fallback;
        }

        public static Color Blend(Color baseColor, Color overlay)
        {
            double a = overlay.A / 255.0;
            int r = BlendCanal((int)(overlay.R * a + baseColor.R * (1 - a)));
            int g = BlendCanal((int)(overlay.G * a + baseColor.G * (1 - a)));
            int b = BlendCanal((int)(overlay.B * a + baseColor.B * (1 - a)));
            return Color.FromArgb(255, r, g, b);
        }
        private static int BlendCanal(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }

        public static Color Darker(Color c, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(c.A,
                (int)(c.R * (1f - amount)),
                (int)(c.G * (1f - amount)),
                (int)(c.B * (1f - amount)));
        }

        public static Color Lighter(Color c, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(c.A,
                (int)(c.R + (255f - c.R) * amount),
                (int)(c.G + (255f - c.G) * amount),
                (int)(c.B + (255f - c.B) * amount));
        }

        // Percurs cu colturi rotunjite pentru efectul de "glow" in jurul unui
        // control. Se deseneaza de mai multe ori cu pen de grosime descrescatoare.
        public static GraphicsPath GlowPath(Rectangle r, int radius, int thickness)
        {
            int t = Math.Max(1, thickness);
            return RoundedPath(new Rectangle(
                r.X - t, r.Y - t,
                Math.Max(1, r.Width + t * 2), Math.Max(1, r.Height + t * 2)),
                radius + t);
        }

        // ── Stilizare unica pentru tabele ─────────────────────────
        public static void StyleGrid(DataGridView grid, Color headerColor)
        {
            if (grid == null) return;
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Surface;
            grid.GridColor = Divider;
            grid.EnableHeadersVisualStyles = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.ReadOnly = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 36;
            grid.RowTemplate.Height = 30;
            grid.Font = FontBody;

            grid.ColumnHeadersDefaultCellStyle.BackColor = headerColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = FontLabel;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerColor;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0x3B, 0x2A, 0x6B); // violet selection
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

            grid.AlternatingRowsDefaultCellStyle.BackColor = Background;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0x3B, 0x2A, 0x6B);
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;
        }
    }

    internal enum MaterialButtonStyle
    {
        Primary,
        Secondary,
        Outline,
        Danger,
        Success,
        Warning,
        Text
    }

    /// <summary>
    /// Buton Material cu OnPaint custom. Textul este desenat manual
    /// cu TextRenderer.DrawText, deci este vizibil in orice stare
    /// (normal / hover / pressed / disabled) — ocoleste bug-ul
    /// FlatStyle.Flat + BackColor + ForeColor.White.
    /// </summary>
    internal class MaterialButton : Button
    {
        public MaterialButtonStyle ButtonStyle { get; set; } = MaterialButtonStyle.Primary;
        public int CornerRadius { get; set; } = MaterialTheme.ButtonRadius;
        private bool _hover;
        private bool _pressed;

        public MaterialButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);   // necesar pt. desenare stabilă
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Empty;
            FlatAppearance.MouseDownBackColor = Color.Empty;
            BackColor = MaterialTheme.Surface;                            // opac, NU Transparent
            Font = MaterialTheme.FontButton;
            Cursor = Cursors.Hand;
            Height = 38;
            Padding = new Padding(12, 0, 12, 0);
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); MaterialTheme.ApplyRounded(this, CornerRadius); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MaterialTheme.ApplyRounded(this, CornerRadius); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color bg = Theme.ResolveOpaqueBackground(this, MaterialTheme.Surface);
            g.Clear(bg);

            Color back, fore, border;
            bool solid = ResolveColors(bg, out back, out fore, out border);

            var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));

            if (!Enabled)
            {
                back = Theme.Blend(bg, Color.FromArgb(70, 90, 100, 120));
                fore = MaterialTheme.TextSecondary;
                border = back;
                solid = false;
            }

            using (var path = MaterialTheme.RoundedPath(rect, CornerRadius))
            {
                if (solid)
                {
                    // Glow neon la hover/pressed în jurul butonului solid.
                    if (_hover || _pressed)
                    {
                        Color glow = MaterialTheme.Lighter(back, 0.18f);
                        for (int i = 3; i >= 1; i--)
                        {
                            using (var gp = MaterialTheme.GlowPath(rect, CornerRadius, i))
                            using (var pen = new Pen(Color.FromArgb(40 / i, glow), 3f))
                                g.DrawPath(pen, gp);
                        }
                    }

                    // Gradient vertical: luminos sus -> culoare de bază jos.
                    using (var lg = new LinearGradientBrush(rect,
                        MaterialTheme.Lighter(back, _pressed ? -0.0f : 0.22f),
                        _pressed ? MaterialTheme.Darker(back, 0.14f) : back,
                        LinearGradientMode.Vertical))
                        g.FillPath(lg, path);

                    using (var pen = new Pen(MaterialTheme.Lighter(back, 0.30f), 1f))
                        g.DrawPath(pen, path);
                }
                else
                {
                    using (var brush = new SolidBrush(back))
                        g.FillPath(brush, path);
                    if (border.A > 16 && border.ToArgb() != back.ToArgb())
                        using (var pen = new Pen(border, 1f))
                            g.DrawPath(pen, path);
                }
            }

            TextRenderer.DrawText(g, Text ?? string.Empty, Font, ClientRectangle, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private bool ResolveColors(Color bg, out Color back, out Color fore, out Color border)
        {
            switch (ButtonStyle)
            {
                case MaterialButtonStyle.Primary:
                    back = _pressed ? MaterialTheme.PrimaryDark
                         : (_hover ? MaterialTheme.PrimaryDark : MaterialTheme.Primary);
                    fore = Color.White; border = back; return true;

                case MaterialButtonStyle.Secondary:
                    back = _hover ? MaterialTheme.SurfaceAlt : MaterialTheme.Surface;
                    fore = MaterialTheme.TextPrimary; border = MaterialTheme.Divider; return false;

                case MaterialButtonStyle.Outline:
                    back = _hover ? Color.FromArgb(38, MaterialTheme.Accent) : MaterialTheme.Surface;
                    fore = MaterialTheme.Accent; border = MaterialTheme.Accent; return false;

                case MaterialButtonStyle.Danger:
                    back = _pressed ? MaterialTheme.Darker(MaterialTheme.Danger, 0.16f)
                         : (_hover ? MaterialTheme.Darker(MaterialTheme.Danger, 0.10f) : MaterialTheme.Danger);
                    fore = Color.White; border = back; return true;

                case MaterialButtonStyle.Success:
                    back = _pressed ? MaterialTheme.Darker(MaterialTheme.Success, 0.16f)
                         : (_hover ? MaterialTheme.Darker(MaterialTheme.Success, 0.10f) : MaterialTheme.Success);
                    fore = Color.White; border = back; return true;

                case MaterialButtonStyle.Warning:
                    back = _pressed ? MaterialTheme.Darker(MaterialTheme.Warning, 0.16f)
                         : (_hover ? MaterialTheme.Darker(MaterialTheme.Warning, 0.10f) : MaterialTheme.Warning);
                    fore = Color.White; border = back; return true;

                case MaterialButtonStyle.Text:
                    back = _hover ? MaterialTheme.SurfaceAlt : bg;
                    fore = MaterialTheme.Accent; border = Color.Transparent; return false;

                default:
                    back = MaterialTheme.Primary; fore = Color.White; border = back; return true;
            }
        }
    }

    /// <summary>
    /// Card petrol cu colturi rotunjite si umbra moale in 5 straturi
    /// (umbra neagra functioneaza pe fundalul inchis "Lobby Black").
    /// </summary>
    internal class MaterialCard : Panel
    {
        public int CornerRadius { get; set; } = MaterialTheme.CardRadius;

        public MaterialCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = MaterialTheme.Surface;
            Padding = new Padding(18);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : MaterialTheme.Background);

            const int shadow = 4;
            var card = new Rectangle(shadow, shadow, Math.Max(1, Width - shadow * 2 - 1), Math.Max(1, Height - shadow * 2 - 1));

            // Umbra moale: 5 straturi cu alfa crescatoare spre card.
            int[] alphas = { 6, 9, 12, 16, 20 };
            for (int i = alphas.Length - 1; i >= 0; i--)
            {
                int pad = shadow - i - 1;
                if (pad < 0) pad = 0;
                var r = new Rectangle(card.X - (shadow - pad) / 2, card.Y - (shadow - pad) / 2,
                    card.Width + (shadow - pad), card.Height + (shadow - pad));
                using (var path = MaterialTheme.RoundedPath(r, CornerRadius + 2))
                using (var brush = new SolidBrush(Color.FromArgb(alphas[alphas.Length - 1 - i], 0, 0, 0)))
                    g.FillPath(brush, path);
            }

            using (var path = MaterialTheme.RoundedPath(card, CornerRadius))
            {
                using (var brush = new SolidBrush(MaterialTheme.Surface))
                    g.FillPath(brush, path);
                using (var pen = new Pen(MaterialTheme.Divider, 1f))
                    g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// Card de meniu clicabil: icon (emoji) + titlu + subtitlu,
    /// banda de accent in stanga, hover cu evidentiere.
    /// </summary>
    internal class MenuCard : Panel
    {
        private string _icon = "";
        private string _title = "";
        private string _subtitle = "";
        private Color _accent = MaterialTheme.Primary;
        private Color _accentGlow = MaterialTheme.PrimaryLight;
        private bool _hover;

        public string Icon { get { return _icon; } set { _icon = value; Invalidate(); } }
        public string CardTitle { get { return _title; } set { _title = value; Invalidate(); } }
        public string Subtitle { get { return _subtitle; } set { _subtitle = value; Invalidate(); } }
        public Color Accent { get { return _accent; } set { _accent = value; Invalidate(); } }
        public Color AccentGlow { get { return _accentGlow; } set { _accentGlow = value; Invalidate(); } }

        public MenuCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = MaterialTheme.Surface;
            Cursor = Cursors.Hand;
            Size = new Size(220, 110);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Parent != null ? Parent.BackColor : MaterialTheme.Background);

            var rect = new Rectangle(1, 1, Math.Max(2, Width - 3), Math.Max(2, Height - 3));

            // Glow neon în jurul cardului la hover.
            if (_hover)
            {
                for (int i = 3; i >= 1; i--)
                {
                    using (var gp = MaterialTheme.GlowPath(rect, MaterialTheme.CardRadius, i))
                    using (var pen = new Pen(Color.FromArgb(34 / i, _accent), 3f))
                        g.DrawPath(pen, gp);
                }
            }

            using (var path = MaterialTheme.RoundedPath(rect, MaterialTheme.CardRadius))
            {
                if (_hover)
                {
                    // Gradient petrol: SurfaceAlt (sus) -> Surface (jos).
                    using (var lg = new LinearGradientBrush(rect,
                        MaterialTheme.SurfaceAlt, MaterialTheme.Surface, LinearGradientMode.Vertical))
                        g.FillPath(lg, path);
                }
                else
                {
                    using (var brush = new SolidBrush(MaterialTheme.Surface))
                        g.FillPath(brush, path);
                }
                using (var pen = new Pen(_hover ? _accent : MaterialTheme.Divider, _hover ? 1.5f : 1f))
                    g.DrawPath(pen, path);
            }

            // Banda accent stanga, cu glow slab
            using (var brush = new SolidBrush(_accent))
            {
                var stripe = new Rectangle(rect.X + 8, rect.Y + 12, 4, rect.Height - 24);
                using (var path = MaterialTheme.RoundedPath(stripe, 2))
                    g.FillPath(brush, path);
            }

            int x = 30;
            if (!string.IsNullOrEmpty(_icon))
                MaterialTheme.DrawGlyph(g, _icon, new Point(x, 12), 20f,
                    _hover ? _accent : MaterialTheme.TextSecondary);
            TextRenderer.DrawText(g, _title ?? string.Empty, MaterialTheme.FontSubtitle,
                new Rectangle(x, 48, Width - x - 12, 24), MaterialTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, _subtitle ?? string.Empty, MaterialTheme.FontSmall,
                new Rectangle(x, 72, Width - x - 12, 30), MaterialTheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
        }
    }

    /// <summary>
    /// Fabrica de layout Material: header / toolbar / bottom bars /
    /// campuri cu eticheta / stilizare grid. Toate panourile folosesc
    /// Dock (Top / Bottom / Fill) ca sa nu existe suprapuneri la resize.
    /// Ordinea corecta de adaugare pe forma:
    ///   Controls.Add(contentPanel);  // Dock.Fill — primul
    ///   Controls.Add(bottomBar);     // Dock.Bottom
    ///   Controls.Add(topToolbar);    // Dock.Top
    ///   Controls.Add(header);        // Dock.Top (deasupra toolbar-ului)
    /// </summary>
    internal static class MaterialUIHelper
    {
        // ── Accesibilitate: tooltip-uri cache-uite pe formă ───────
        // Un singur ToolTip per formă (evită leak-ul GDI al mai multor
        // instanțe). Eliberat la FormClosed al formei gazdă.
        private static readonly Dictionary<Form, ToolTip> _tooltips =
            new Dictionary<Form, ToolTip>();

        /// <summary>
        /// Atașează tooltip + metadate de accesibilitate (<see cref="Control.AccessibleName"/>,
        /// <see cref="Control.AccessibleDescription"/>) unui control. ToolTip-ul este
        /// partajat pe formă și eliberat automat la FormClosed.
        /// </summary>
        public static void AtasareTooltip(Control control, string titlu, string text)
        {
            if (control == null || string.IsNullOrEmpty(text)) return;
            Form form = control.FindForm();
            if (form == null) return;

            ToolTip tt;
            lock (_tooltips)
            {
                if (!_tooltips.TryGetValue(form, out tt))
                {
                    tt = new ToolTip
                    {
                        AutoPopDelay = 8000,
                        InitialDelay = 400,
                        ReshowDelay = 120,
                        ShowAlways = true
                    };
                    _tooltips[form] = tt;
                    form.FormClosed += (s, e) =>
                    {
                        lock (_tooltips)
                        {
                            tt.Dispose();
                            _tooltips.Remove(form);
                        }
                    };
                }
            }

            tt.SetToolTip(control, string.IsNullOrEmpty(titlu) ? text : titlu + "\n" + text);
            if (string.IsNullOrEmpty(control.AccessibleName))
                control.AccessibleName = string.IsNullOrEmpty(titlu) ? text : titlu;
            control.AccessibleDescription = text;
        }

        public static MaterialButton Button(string text, MaterialButtonStyle style, int width)
        {
            return new MaterialButton
            {
                Text = text,
                ButtonStyle = style,
                Size = new Size(width, 38),
                Margin = new Padding(0, 0, 10, 0)
            };
        }

        public static MaterialButton Button(string text, MaterialButtonStyle style, int width, int height)
        {
            return new MaterialButton
            {
                Text = text,
                ButtonStyle = style,
                Size = new Size(width, height),
                Margin = new Padding(0, 0, 10, 0)
            };
        }

        public static Panel Header(string title, string subtitle, string userInfo, Color color)
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = MaterialTheme.HeaderH,
                BackColor = color,
                Padding = new Padding(20, 10, 20, 10)
            };
            header.Paint += (s, e) =>
            {
                using (var pen = new Pen(MaterialTheme.Accent, 2f))
                    e.Graphics.DrawLine(pen, 0, header.Height - 2, header.Width, header.Height - 2);
            };
            var lblTitle = new Label
            {
                Text = title,
                Font = MaterialTheme.FontTitle,
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 10),
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblTitle);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var lblSub = new Label
                {
                    Text = subtitle,
                    Font = MaterialTheme.FontSmall,
                    ForeColor = Color.FromArgb(220, 255, 255, 255),
                    AutoSize = true,
                    Location = new Point(21, 40),
                    BackColor = Color.Transparent
                };
                header.Controls.Add(lblSub);
            }

            if (!string.IsNullOrEmpty(userInfo))
            {
                var lblUser = new Label
                {
                    Text = userInfo,
                    Font = MaterialTheme.FontLabel,
                    ForeColor = Color.White,
                    AutoSize = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    BackColor = Color.Transparent
                };
                header.Controls.Add(lblUser);
                header.Resize += (s, e) => { lblUser.Location = new Point(Math.Max(0, header.Width - lblUser.Width - 20), 26); };
                lblUser.Location = new Point(800, 26);
            }
            return header;
        }

        public static Panel Toolbar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = MaterialTheme.TopBarH,
                BackColor = MaterialTheme.Surface,
                Padding = new Padding(12, 11, 12, 11)
            };
            bar.Paint += (s, e) =>
            {
                using (var pen = new Pen(MaterialTheme.Divider, 1f))
                    e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };
            return bar;
        }

        public static Panel ContentPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = MaterialTheme.Background,
                Padding = new Padding(MaterialTheme.ContentPad),
                AutoScroll = true
            };
        }

        public static Panel BottomBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = MaterialTheme.BottomBarH,
                BackColor = MaterialTheme.BottomBar,
                Padding = new Padding(12, 12, 12, 12)
            };
            bar.Paint += (s, e) =>
            {
                using (var pen = new Pen(MaterialTheme.Divider, 1f))
                    e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            bar.Controls.Add(flow);
            return bar;
        }

        /// <summary>
        /// Bara inferioara cu doua zone: stanga (operatii pe inregistrari)
        /// si dreapta (actiuni generale). Intoarce bara + cele doua flow-uri.
        /// </summary>
        public static Panel SplitBottomBar(out Panel left, out FlowLayoutPanel rightFlow)
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = MaterialTheme.BottomBarH,
                BackColor = MaterialTheme.BottomBar,
                Padding = new Padding(12, 12, 12, 12)
            };
            bar.Paint += (s, e) =>
            {
                using (var pen = new Pen(MaterialTheme.Divider, 1f))
                    e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };

            var leftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            var right = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            bar.Controls.Add(leftFlow);
            bar.Controls.Add(right);

            left = leftFlow;
            rightFlow = right;
            return bar;
        }

        public static Panel SplitBottomBar(out FlowLayoutPanel leftFlow, out FlowLayoutPanel rightFlow2)
        {
            Panel dummy;
            FlowLayoutPanel right;
            var bar = SplitBottomBar(out dummy, out right);
            leftFlow = (FlowLayoutPanel)dummy;
            rightFlow2 = right;
            return bar;
        }

        /// <summary>
        /// Camp text cu eticheta deasupra (fara decupaje): eticheta 9pt Bold
        /// gri, TextBox 28px cu border Divider. Returneaza TextBox-ul creat.
        /// y este coordonata de start; inaltimea ocupata = 18 (label) + 30 (field) + 10 gap.
        /// </summary>
        public static TextBox LabeledTextBox(string labelText, Control parent, int y)
        {
            return LabeledTextBox(labelText, parent, y, 20, 280, false);
        }

        public static TextBox LabeledTextBox(string labelText, Control parent, int y, int x, int width, bool multiline)
        {
            var lbl = new Label
            {
                Text = labelText,
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = System.Drawing.Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(width, 18)
            };
            parent.Controls.Add(lbl);

            var txt = new TextBox
            {
                Location = new Point(x, y + 20),
                Size = new Size(width, multiline ? 52 : 28),
                Multiline = multiline,
                Font = MaterialTheme.FontBody,
                ForeColor = MaterialTheme.TextPrimary,
                BackColor = MaterialTheme.SurfaceAlt,
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            parent.Controls.Add(txt);
            return txt;
        }

        public static int LabeledTextBox(string labelText, Control parent, ref int y, int x, int width, bool multiline, out TextBox created)
        {
            created = LabeledTextBox(labelText, parent, y, x, width, multiline);
            int used = multiline ? (20 + 52 + 12) : (20 + 28 + 12);
            y += used;
            return y;
        }

        public static void StyleGrid(DataGridView dgv, Color headerColor)
        {
            MaterialTheme.StyleGrid(dgv, headerColor);
        }
    }
}
