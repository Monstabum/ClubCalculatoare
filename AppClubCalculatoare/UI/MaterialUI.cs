using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Paleta și metricele istorice ale aplicației. Clasa este PĂSTRATĂ
    /// pentru compatibilitate (există apeluri în toate formularele), dar
    /// toate valorile sunt acum redirecționate către <see cref="Design"/> —
    /// un singur accent cyan, suprafețe gri neutre, fără culori decorative.
    /// Cod nou trebuie să folosească direct <see cref="Design"/>.
    /// </summary>
    internal static class MaterialTheme
    {
        // -- Paleta semantica (redirectionata catre Design) --------
        public static readonly Color Primary      = Design.Accent;
        public static readonly Color PrimaryDark  = Design.AccentMuted;
        public static readonly Color PrimaryLight = Design.AccentHover;
        public static readonly Color Accent       = Design.Accent;

        public static readonly Color Success = Design.Success;
        public static readonly Color Danger  = Design.Danger;
        public static readonly Color Warning = Design.Warning;
        public static readonly Color Info    = Design.Info;

        // -- Variante luminoase pentru TEXT pe fundal inchis ------
        // Sunt folosite doar pentru ForeColor, niciodata ca fundal.
        public static readonly Color SuccessBright = Design.SuccessBright;
        public static readonly Color DangerBright  = Design.DangerBright;
        public static readonly Color WarningBright = Design.WarningBright;
        public static readonly Color InfoBright    = Color.FromArgb(0x60, 0xA5, 0xFA);

        // -- Fundale pentru evidentiere pe grile inchise ----------
        public static readonly Color HighlightWarning = Color.FromArgb(0x2A, 0x21, 0x0D);
        public static readonly Color HighlightDanger  = Color.FromArgb(0x2C, 0x14, 0x14);
        public static readonly Color HighlightAccent  = Color.FromArgb(0x0B, 0x2B, 0x33);

        public static readonly Color Background = Design.Bg;
        public static readonly Color Surface    = Design.Surface;
        public static readonly Color SurfaceAlt = Design.SurfaceRaised;
        public static readonly Color BottomBar  = Design.Surface;
        public static readonly Color SidebarBg  = Design.Bg;

        public static readonly Color TextPrimary   = Design.Text;
        public static readonly Color TextSecondary = Design.TextMuted;
        public static readonly Color Divider       = Design.Divider;

        // -- Tonuri „soft" (migrate din Theme, folosite pe fundal inchis) --
        public static readonly Color TextDim      = Design.TextDim;
        public static readonly Color BorderStrong = Design.Border;
        public static readonly Color AccentSoft   = Design.AccentMuted;
        public static readonly Color WarningSoft  = Color.FromArgb(0x2A, 0x21, 0x0D);

        // -- Nume istorice pastrate ca aliasuri --------------------
        // (fostele accente decorative violet/albastru/magenta sunt acum
        //  reduse la singurul accent cyan al temei)
        public static readonly Color NeonViolet     = Design.Accent;
        public static readonly Color Indigo         = Design.AccentMuted;
        public static readonly Color NeonPink       = Design.Accent;
        public static readonly Color NeonPinkBright = Design.AccentHover;
        public static readonly Color Glass          = Design.SurfaceRaised;
        public static readonly Color GlassBorder    = Design.Border;
        public static readonly Color Sheen          = Design.Divider;
        public static readonly Color ShadowTint     = Design.Bg;

        // -- Dimensiuni --------------------------------------------
        public const int ButtonRadius = Design.RadiusSmall;
        public const int CardRadius   = Design.Radius;
        public const int TopBarH      = Design.TopBarH;
        public const int BottomBarH   = Design.BottomBarH;
        public const int HeaderH      = 64;
        public const int ContentPad   = Design.Sp5;
        public const int SidebarW     = Design.SidebarW;

        // -- Tipografie (Segoe UI) ---------------------------------
        public const string FontFamily = Design.FontFamily;

        // -- Scalarea dimensiunilor pentru DPI ---------------------
        // Valorile din tema sunt date pentru 96 DPI; la DPI mai mare
        // sunt multiplicate prin MaterialTheme.Scale (vezi mai jos).

        /// <summary>Creează un font Segoe UI la dimensiunea dată.</summary>
        public static Font GetFont(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(FontFamily, size, style);
        }

        public static Font FontTitle    => Design.FontTitle;
        public static Font FontSubtitle => Design.FontHeading;
        public static Font FontBody     => Design.FontBody;
        public static Font FontSmall    => Design.FontSubtitle;
        public static Font FontButton   => Design.FontButton;
        public static Font FontLabel    => Design.FontLabel;

        // -- Alias tipografice (migrate din Theme; acum din scara Design) --
        public static Font H1             => Design.FontDisplay;
        public static Font H2             => Design.FontHeading;
        public static Font H3             => Design.SemiBold(9.5f);
        public static Font Body           => Design.FontBody;
        public static Font BodyBold       => Design.FontBodyBold;
        public static Font Small          => Design.FontSmall;
        public static Font SmallBold      => Design.FontSmallBold;
        public static Font Caption        => Design.FontCaption;
        public static Font CaptionItalic  => Design.FontCaptionItalic;
        public static Font NavFont        => Design.FontNav;
        public static Font NavFontActive  => Design.FontNavActive;
        public static Font KpiValue       => Design.FontKpiValue;
        public static Font KpiUnit        => Design.FontKpiUnit;

        // -- Helper-e de culoare -----------------------------------
        // Nota: Blend / Lighter / Darker exista deja mai jos (varianta cu overlay alpha
        // pentru butoane). Aici adaugam doar ajutoarele care lipsesc.

        /// <summary>Adauga transparen?a (alpha 0..255) unei culori.</summary>
        public static Color WithAlpha(Color c, int alpha) => Color.FromArgb(alpha, c);

        /// <summary>Familia de glife pentru pictogramele din interfață.</summary>
        private static string _glyphFont;

        /// <summary>
        /// Fontul pentru glife PUA (\uE...): "Segoe Fluent Icons" (Win11),
        /// cu fallback la "Segoe MDL2 Assets" (Win10+). NU "Segoe UI Symbol":
        /// in Symbol aceste coduri nu exista si toate pictogramele (sidebar,
        /// carduri KPI, harta salii, lupa de cautare) apareau ca dreptunghiuri.
        /// </summary>
        public static string GlyphFont
        {
            get
            {
                if (_glyphFont == null)
                {
                    _glyphFont = "Segoe Fluent Icons";
                    try
                    {
                        using (var fc = new System.Drawing.Text.InstalledFontCollection())
                        {
                            bool fluent = false, mdl2 = false;
                            foreach (var f in fc.Families)
                            {
                                if (f.Name.Equals("Segoe Fluent Icons", System.StringComparison.OrdinalIgnoreCase)) fluent = true;
                                else if (f.Name.Equals("Segoe MDL2 Assets", System.StringComparison.OrdinalIgnoreCase)) mdl2 = true;
                            }
                            if (!fluent && mdl2) _glyphFont = "Segoe MDL2 Assets";
                        }
                    }
                    catch { _glyphFont = "Segoe Fluent Icons"; }
                }
                return _glyphFont;
            }
        }

        /// <summary>
        /// Deseneaza un glif din fontul <see cref="GlyphFont"/> (Fluent/MDL2),
        /// centrat pe inaltime intr-un rand la pozitia data.
        /// Render independent de locala (spre deosebire de fontul Emoji).
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
        /// Scaleaza o valoare din pixeli logici (96 DPI) la DPI-ul ecranului
        /// curent. Permite controalelor construite in cod (inal?imi/la?imi
        /// hardcodate) sa urmeze scalarea Windows pe 125% / 150%.
        /// Fara ecran/GDI ramane valoarea logica.
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
                return px;   // fara ecran/GDI — ramane valoarea logica
            }
        }

        // -- Helper: regiune cu colturi rotunjite ------------------
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

        // -- Rezolvare fundal opac + amestec culori (canonic, vezi Theme) --
        // Implementarea unica a ajutoarelor folosite de butoanele custom
        // (inainte duplicata in Theme). Theme.* redirectioneaza aici.
        public static Color ResolveOpaqueBackground(Control control, Color fallback)
        {
            // Pornim de la PĂRINTE, nu de la control: pentru colțurile rotunjite
            // avem nevoie de culoarea din SPATELE controlului (altfel colțurile
            // ar rămâne în culoarea proprie de fundal, ca niște pătrățele).
            for (Control c = control != null ? control.Parent : null; c != null; c = c.Parent)
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


        // -- Stilizare unica pentru tabele -------------------------
        /// <summary>
        /// Stilul unic al tabelelor: suprafață <see cref="Design.Surface"/> cu
        /// rânduri alternate <see cref="Design.Bg"/>, antet discret pe
        /// <see cref="Design.SurfaceRaised"/>, separatoare fine și selecție
        /// cyan translucidă. Fără chenar exterior.
        ///
        /// <paramref name="headerColor"/> este păstrat doar pentru
        /// compatibilitatea apelurilor existente — noul design folosește
        /// o singură culoare de antet, indiferent de modul formularului.
        /// </summary>
        public static void StyleGrid(DataGridView grid, Color headerColor)
        {
            if (grid == null) return;

            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Design.Surface;
            grid.GridColor = Design.Divider;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.EnableHeadersVisualStyles = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.ReadOnly = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 32;
            grid.RowTemplate.Height = 30;
            grid.Font = Design.FontSubtitle;

            // Antet: fără chenar, suprafață ridicată, text discret uppercase.
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Design.SurfaceRaised;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Design.TextMuted;
            grid.ColumnHeadersDefaultCellStyle.Font = Design.FontSmallBold;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Design.SurfaceRaised;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Design.TextMuted;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(Design.Sp2, 0, Design.Sp2, 0);

            grid.DefaultCellStyle.BackColor = Design.Surface;
            grid.DefaultCellStyle.ForeColor = Design.Text;
            grid.DefaultCellStyle.SelectionBackColor = Design.AccentMuted;
            grid.DefaultCellStyle.SelectionForeColor = Design.Text;
            grid.DefaultCellStyle.Padding = new Padding(Design.Sp2, 0, Design.Sp2, 0);

            grid.AlternatingRowsDefaultCellStyle.BackColor = Design.Bg;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = Design.Text;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Design.AccentMuted;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Design.Text;

            // Etichetele de coloană se afișează uppercase (o singură dată,
            // la generarea coloanelor) — limbajul vizual al antetului.
            grid.ColumnAdded -= UppercaseColumnHeader;
            grid.ColumnAdded += UppercaseColumnHeader;
        }

        private static void UppercaseColumnHeader(object sender, DataGridViewColumnEventArgs e)
        {
            if (e.Column == null || string.IsNullOrEmpty(e.Column.HeaderText)) return;
            e.Column.HeaderText = e.Column.HeaderText.ToUpperInvariant();
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
    /// Buton plat (fără umbră, fără gradient, fără glow). Textul este desenat
    /// manual cu TextRenderer.DrawText, deci este vizibil în orice stare
    /// (normal / hover / pressed / disabled) — ocolește bug-ul
    /// FlatStyle.Flat + BackColor + ForeColor.White.
    /// </summary>
    internal class MaterialButton : Button
    {
        [DefaultValue(MaterialButtonStyle.Primary)]
        public MaterialButtonStyle ButtonStyle { get; set; } = MaterialButtonStyle.Primary;
        [DefaultValue(Design.RadiusSmall)]
        public int CornerRadius { get; set; } = Design.RadiusSmall;
        private bool _hover;
        private bool _pressed;

        public MaterialButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);   // necesar pt. desenare stabila
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Empty;
            FlatAppearance.MouseDownBackColor = Color.Empty;
            BackColor = Design.Surface;                                   // opac, NU Transparent
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
                // Umplere plată (o singură culoare — fără gradient).
                if (back.A > 0)
                    using (var brush = new SolidBrush(back))
                        g.FillPath(brush, path);

                // Hover pentru variantele „ghost": strat translucid peste suprafață.
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

        /// <summary>
        /// Rezolvă culorile variantei curente. <paramref name="overlay"/> este
        /// stratul translucid desenat peste fundal la hover (Transparent = niciunul).
        /// </summary>
        private void ResolveColors(out Color back, out Color fore,
            out Color border, out Color overlay)
        {
            bool activ = _hover || _pressed;
            overlay = Color.Transparent;

            switch (ButtonStyle)
            {
                case MaterialButtonStyle.Primary:
                    back = activ ? Design.AccentHover : Design.Accent;
                    fore = Design.AccentText;
                    border = Color.Transparent;
                    return;

                case MaterialButtonStyle.Secondary:
                    back = Design.SurfaceRaised;
                    fore = Design.Text;
                    border = Design.Border;
                    overlay = _hover ? Design.Alpha(Design.Text, 10) : Color.Transparent;
                    return;

                case MaterialButtonStyle.Outline:
                    back = Color.Transparent;
                    fore = _hover ? Design.Text : Design.TextMuted;
                    border = Design.Border;
                    overlay = _hover ? Design.Alpha(Color.White, 6) : Color.Transparent;
                    return;

                case MaterialButtonStyle.Danger:
                    back = activ ? Design.DangerBright : Design.Danger;
                    fore = Color.White;
                    border = Color.Transparent;
                    return;

                case MaterialButtonStyle.Success:
                    back = activ ? Design.SuccessBright : Design.Success;
                    fore = Color.White;
                    border = Color.Transparent;
                    return;

                case MaterialButtonStyle.Warning:
                    back = activ ? Design.WarningBright : Design.Warning;
                    fore = Design.AccentText;
                    border = Color.Transparent;
                    return;

                case MaterialButtonStyle.Text:
                    back = Color.Transparent;
                    fore = Design.Accent;
                    border = Color.Transparent;
                    overlay = _hover ? Design.Alpha(Color.White, 6) : Color.Transparent;
                    return;

                default:
                    back = Design.Accent;
                    fore = Design.AccentText;
                    border = Color.Transparent;
                    return;
            }
        }
    }

    /// <summary>
    /// Card plat: suprafață <see cref="Design.Surface"/> + contur 1px
    /// <see cref="Design.Border"/>, colțuri rotunjite. Fără umbră, fără
    /// gradient, fără linie de accent — ierarhia vine din suprafață.
    /// </summary>
    internal class MaterialCard : Panel
    {
        [DefaultValue(Design.Radius)]
        public int CornerRadius { get; set; } = Design.Radius;

        public MaterialCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Design.Surface;
            Padding = new Padding(Design.Sp5);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Design.ResolveOpaqueBackground(this, Design.Bg));

            var rect = new Rectangle(0, 0, Math.Max(2, Width - 1), Math.Max(2, Height - 1));
            Design.PaintCard(g, rect, CornerRadius);
        }
    }

    /// <summary>
    /// Card de meniu clicabil: glif + titlu + subtitlu, pe o suprafață
    /// neutră. Fără bandă de accent, fără gradient, fără glow — la hover
    /// doar suprafața urcă un nivel și conturul devine mai vizibil.
    /// </summary>
    internal class MenuCard : Panel
    {
        private string _icon = "";
        private string _title = "";
        private string _subtitle = "";
        private bool _hover;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Icon { get { return _icon; } set { _icon = value; Invalidate(); } }
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CardTitle { get { return _title; } set { _title = value; Invalidate(); } }
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Subtitle { get { return _subtitle; } set { _subtitle = value; Invalidate(); } }

        /// <summary>Păstrat pentru compatibilitate — fără efect vizual.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color Accent { get; set; } = Design.Accent;

        /// <summary>Păstrat pentru compatibilitate — fără efect vizual.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentGlow { get; set; } = Design.AccentHover;

        public MenuCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Design.Surface;
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
            g.Clear(Design.ResolveOpaqueBackground(this, Design.Bg));

            var rect = new Rectangle(0, 0, Math.Max(2, Width - 1), Math.Max(2, Height - 1));

            using (var path = Design.RoundedPath(rect, Design.Radius))
            {
                using (var brush = new SolidBrush(_hover ? Design.SurfaceRaised : Design.Surface))
                    g.FillPath(brush, path);
                using (var pen = new Pen(Design.Border, 1f))
                    g.DrawPath(pen, path);
            }

            // Glif pe un pătrat rotunjit neutru (fără chip colorat).
            var chip = new Rectangle(rect.X + Design.Sp4, rect.Y + Design.Sp4, 32, 32);
            using (var path = Design.RoundedPath(chip, Design.RadiusSmall))
            using (var brush = new SolidBrush(Design.Bg))
                g.FillPath(brush, path);

            if (!string.IsNullOrEmpty(_icon))
                MaterialTheme.DrawGlyph(g, _icon,
                    new Point(chip.X + 7, chip.Y + 7), 15f,
                    _hover ? Design.Text : Design.TextMuted);

            int x = chip.Right + Design.Sp3;
            int w = Math.Max(20, rect.Right - x - Design.Sp3);

            TextRenderer.DrawText(g, _title ?? string.Empty, Design.FontHeading,
                new Rectangle(x, rect.Y + Design.Sp4, w, 20), Design.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, _subtitle ?? string.Empty, Design.FontSmall,
                new Rectangle(x, rect.Y + Design.Sp4 + 24, w, 40), Design.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.WordBreak);
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
        // -- Accesibilitate: tooltip-uri cache-uite pe forma -------
        // Un singur ToolTip per forma (evita leak-ul GDI al mai multor
        // instan?e). Eliberat la FormClosed al formei gazda.
        private static readonly Dictionary<Form, ToolTip> _tooltips =
            new Dictionary<Form, ToolTip>();

        /// <summary>
        /// Ata?eaza tooltip + metadate de accesibilitate (<see cref="Control.AccessibleName"/>,
        /// <see cref="Control.AccessibleDescription"/>) unui control. ToolTip-ul este
        /// partajat pe forma ?i eliberat automat la FormClosed.
        /// Intoarce false daca textul e gol sau controlul nu e inca ata?at la
        /// o forma (apeleaza atunci din nou mai tarziu, ex. din ParentChanged).
        /// </summary>
        public static bool AtasareTooltip(Control control, string titlu, string text)
        {
            if (control == null || string.IsNullOrEmpty(text)) return false;
            Form form = control.FindForm();
            if (form == null) return false;

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
            return true;
        }

        /// <summary>
        /// Aplica tema inchisa unui control de intrare (TextBox, ComboBox,
        /// NumericUpDown, DateTimePicker).
        ///
        /// Fara asta, controalele native raman la fundalul ALB al sistemului,
        /// iar cu ForeColor alb (TextPrimary) textul intra exact in camp —
        /// efectul opus celui intentionat. Aici se fixeaza perechea
        /// fundal/foreground din paleta canonica.
        ///
        /// Pentru ComboBox e obligatoriu FlatStyle.Flat: altfel ComboBox
        /// ignora BackColor si deseneaza tot fundalul de sistem.
        /// </summary>
        public static void StyleInput(Control control)
        {
            if (control == null) return;

            control.BackColor = Design.SurfaceRaised;
            control.ForeColor = Design.Text;

            var txt = control as TextBox;
            if (txt != null)
            {
                // Nu atingem BorderStyle=None: e folosit intentionat de
                // campurile de cautare cu fundal transparent (searchBox).
                if (txt.BorderStyle == System.Windows.Forms.BorderStyle.Fixed3D ||
                    txt.BorderStyle == System.Windows.Forms.BorderStyle.FixedSingle)
                    txt.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                return;
            }

            var cmb = control as ComboBox;
            if (cmb != null)
            {
                cmb.FlatStyle = FlatStyle.Flat;
                return;
            }
        }

        /// <summary>
        /// Varianta —read-only" a campurilor: fundal mai jos decat cel editabil
        /// (vizibil ca atare ca fiind needitabil) si text tot lizibil.
        /// NU folosim un alb/ gri deschis aici — textul alb al temei ar
        /// deveni invizibil (bug-ul din modul —Vizualizare detalii").
        /// </summary>
        public static void StyleInputReadOnly(Control control)
        {
            StyleInput(control);
            if (control != null)
            {
                control.BackColor = Design.Surface;
                control.ForeColor = Design.TextMuted;
            }
        }

        public static MaterialButton Button(string text, MaterialButtonStyle style, int width)
        {
            return new MaterialButton
            {
                Text = text,
                ButtonStyle = style,
                Size = new Size(width, Design.ButtonH),
                Margin = new Padding(0, 0, Design.Sp2, 0)
            };
        }

        public static MaterialButton Button(string text, MaterialButtonStyle style, int width, int height)
        {
            return new MaterialButton
            {
                Text = text,
                ButtonStyle = style,
                Size = new Size(width, height),
                Margin = new Padding(0, 0, Design.Sp2, 0)
            };
        }

        /// <summary>
        /// Antetul de formular: suprafață neutră <see cref="Design.Surface"/>
        /// cu un separator de 1px <see cref="Design.Border"/> jos. Titlul este
        /// elementul dominant; culoarea primită este ignorată (designul nu
        /// mai codifică tipul formularului prin culoare).
        ///
        /// Înălțimea este calculată din înălțimea reală a fonturilor, ca
        /// antetul să nu fie niciodată tăiat la scalarea DPI.
        /// </summary>
        public static Panel Header(string title, string subtitle, string userInfo, Color color)
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Design.Surface,
                Padding = new Padding(Design.Sp5, Design.Sp3, Design.Sp5, Design.Sp3)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = Design.FontTitle,
                ForeColor = Design.Text,
                AutoSize = true,
                Location = new Point(Design.Sp5, Design.Sp3),
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblTitle);

            int jos = lblTitle.Bottom;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var lblSub = new Label
                {
                    Text = subtitle,
                    Font = Design.FontSubtitle,
                    ForeColor = Design.TextMuted,
                    AutoSize = true,
                    Location = new Point(Design.Sp5, jos + Design.Sp1),
                    BackColor = Color.Transparent
                };
                header.Controls.Add(lblSub);
                jos = lblSub.Bottom;
            }

            header.Height = Math.Max(MaterialTheme.HeaderH, jos + Design.Sp3);
            header.Paint += (s, e) =>
                Design.DrawSeparator(e.Graphics, header.Height - 1, header.Width, Design.Border);

            if (!string.IsNullOrEmpty(userInfo))
            {
                var lblUser = new Label
                {
                    Text = userInfo,
                    Font = Design.FontLabel,
                    ForeColor = Design.TextMuted,
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                header.Controls.Add(lblUser);
                header.Resize += (s, e) =>
                    lblUser.Location = new Point(
                        Math.Max(Design.Sp5, header.Width - lblUser.Width - Design.Sp5),
                        Design.Sp3);
                lblUser.Location = new Point(
                    Math.Max(Design.Sp5, header.Width - lblUser.Width - Design.Sp5), Design.Sp3);
            }
            return header;
        }

        /// <summary>
        /// Aplică aspectul de „search box" unui panou-container: suprafață
        /// <see cref="Design.SurfaceRaised"/>, contur 1px
        /// <see cref="Design.Border"/>, colțuri de <see cref="Design.RadiusSmall"/>.
        /// Identic cu bara de căutare din fereastra principală.
        /// </summary>
        public static void StyleSearchBox(Panel box)
        {
            if (box == null) return;
            box.BackColor = Design.SurfaceRaised;
            box.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Design.SurfaceRaised);
                using (var path = Design.RoundedPath(
                    new Rectangle(0, 0, Math.Max(2, box.Width - 1), Math.Max(2, box.Height - 1)),
                    Design.RadiusSmall))
                using (var pen = new Pen(Design.Border, 1f))
                    g.DrawPath(pen, path);
            };
        }

        public static Panel Toolbar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = Design.TopBarH,
                BackColor = Design.Surface,
                Padding = new Padding(Design.Sp3, Design.Sp3, Design.Sp3, Design.Sp3)
            };
            bar.Paint += (s, e) =>
                Design.DrawSeparator(e.Graphics, bar.Height - 1, bar.Width, Design.Border);
            return bar;
        }

        public static Panel ContentPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Design.Bg,
                Padding = new Padding(MaterialTheme.ContentPad),
                AutoScroll = true
            };
        }

        public static Panel BottomBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = Design.BottomBarH,
                BackColor = Design.Surface,
                Padding = new Padding(Design.Sp3, Design.Sp2, Design.Sp4, Design.Sp2)
            };
            bar.Paint += (s, e) =>
                Design.DrawSeparator(e.Graphics, 0, bar.Width, Design.Border);
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
                Height = Design.BottomBarH,
                BackColor = Design.Surface,
                Padding = new Padding(Design.Sp3, Design.Sp2, Design.Sp4, Design.Sp2)
            };
            bar.Paint += (s, e) =>
                Design.DrawSeparator(e.Graphics, 0, bar.Width, Design.Border);

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
                Font = Design.FontLabel,
                ForeColor = Design.TextMuted,
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
                Font = Design.FontBody,
                ForeColor = Design.Text,
                BackColor = Design.SurfaceRaised,
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
