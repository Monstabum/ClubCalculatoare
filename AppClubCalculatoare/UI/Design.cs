
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    
    
    public static class Design
    {
        // ════════════════════════════════════════════════════════
        //  SUPRAFEȚE — ladder strict (Vercel / Linear)
        // ════════════════════════════════════════════════════════

        /// <summary>Fundal aplicație (întunecat, aproape negru).</summary>
        public static readonly Color Bg = Color.FromArgb(0x09, 0x09, 0x0B);        // #09090b

        /// <summary>Suprafață card / panou principal.</summary>
        public static readonly Color Surface = Color.FromArgb(0x11, 0x11, 0x13);   // #111113

        /// <summary>Suprafață ridicată (toolbar, input, hover).</summary>
        public static readonly Color SurfaceRaised = Color.FromArgb(0x18, 0x18, 0x1B); // #18181b

        /// <summary>Border subtil pentru carduri, inputuri, separatoare.</summary>
        public static readonly Color Border = Color.FromArgb(0x27, 0x27, 0x2A);    // #27272a

        /// <summary>Linie de separare (mai fină decât Border).</summary>
        public static readonly Color Divider = Color.FromArgb(0x1F, 0x1F, 0x22);

        // ════════════════════════════════════════════════════════
        //  TEXT — ladder pe 3 niveluri
        // ════════════════════════════════════════════════════════

        /// <summary>Text principal (titluri, valori KPI, butoane).</summary>
        public static readonly Color Text = Color.FromArgb(0xFA, 0xFA, 0xFA);     // #fafafa

        /// <summary>Text secundar (etichete, subtitluri).</summary>
        public static readonly Color TextMuted = Color.FromArgb(0xA1, 0xA1, 0xAA); // #a1a1aa

        /// <summary>
        /// Alias semantic pentru <see cref="TextMuted"/> — folosit de zonele
        /// care descriu nivelul „al doilea" de text (alerte, subtitluri de
        /// formular). Aceeași valoare, un singur nivel în ladder.
        /// </summary>
        public static readonly Color TextSecondary = TextMuted;

        /// <summary>Text terțiar (meta, hint-uri, placeholder).</summary>
        public static readonly Color TextDim = Color.FromArgb(0x52, 0x52, 0x5B);  // #52525b

        // ════════════════════════════════════════════════════════
        //  ACCENT UNIC — cyan (folosit rar, dar consecvent)
        // ════════════════════════════════════════════════════════

        /// <summary>Culoarea de accent (buton principal, stare activă, link).</summary>
        public static readonly Color Accent = Color.FromArgb(0x06, 0xB6, 0xD4);   // cyan-500

        /// <summary>Accent la hover (mai deschis).</summary>
        public static readonly Color AccentHover = Color.FromArgb(0x22, 0xD3, 0xEE); // cyan-400

        /// <summary>Accent în fundal (badge, highlight slab).</summary>
        public static readonly Color AccentMuted = Color.FromArgb(0x16, 0x4E, 0x63); // cyan-900

        /// <summary>Text pe fundal de accent (negru, contrast 8:1).</summary>
        public static readonly Color AccentText = Color.FromArgb(0x00, 0x00, 0x00);

        // ════════════════════════════════════════════════════════
        //  STĂRI SEMANTICE — doar pe indicatori, nu decorativ
        // ════════════════════════════════════════════════════════

        public static readonly Color Success = Color.FromArgb(0x10, 0xB9, 0x81);       // emerald-500
        public static readonly Color SuccessBright = Color.FromArgb(0x34, 0xD3, 0x99); // emerald-400

        public static readonly Color Warning = Color.FromArgb(0xF5, 0x9E, 0x0B);       // amber-500
        public static readonly Color WarningBright = Color.FromArgb(0xFB, 0xBF, 0x24); // amber-400

        public static readonly Color Danger = Color.FromArgb(0xEF, 0x44, 0x44);        // red-500
        public static readonly Color DangerBright = Color.FromArgb(0xF8, 0x71, 0x71);  // red-400

        public static readonly Color Info = Color.FromArgb(0x3B, 0x82, 0xF6);          // blue-500

        // ════════════════════════════════════════════════════════
        //  METRICI — rază, spațiere (grilă de 4px)
        // ════════════════════════════════════════════════════════

        /// <summary>Rază buton mic, badge (px).</summary>
        public const int RadiusSmall = 6;

        /// <summary>Rază card, input, panou (px).</summary>
        public const int Radius = 8;

        /// <summary>Rază container mare, modale (px).</summary>
        public const int RadiusLarge = 12;

        // Spațiere pe grilă de 4
        public const int Sp1 = 4;
        public const int Sp2 = 8;
        public const int Sp3 = 12;
        public const int Sp4 = 16;
        public const int Sp5 = 20;
        public const int Sp6 = 24;
        public const int Sp8 = 32;

        // Dimensiuni standard pentru bara de chrome
        public const int TopBarH = 56;
        public const int SidebarW = 232;
        public const int BottomBarH = 48;
        public const int ButtonH = 32;

        /// <summary>Înălțimea rândului de meniu din sidebar.</summary>
        public const int NavItemH = 36;

        // ════════════════════════════════════════════════════════
        //  TIPOGRAFIE — o singură familie (Segoe UI) + o scară fixă
        // ════════════════════════════════════════════════════════

        /// <summary>Familia tipografică unică a aplicației.</summary>
        public const string FontFamily = "Segoe UI";

        /// <summary>Familia monospațiată (detaliu tehnic, meta, contoare).</summary>
        public const string FontFamilyMono = "Consolas";

        private static string _familieSemiBold;

        /// <summary>
        /// „Segoe UI Semibold" dacă e instalat, altfel „Segoe UI". Familia
        /// este căutată o singură dată (cache static): dacă lipsește,
        /// <c>new Font</c> ar substitui tăcut un font complet diferit.
        /// </summary>
        private static string FamilieSemiBold
        {
            get
            {
                if (_familieSemiBold == null)
                {
                    _familieSemiBold = FontFamily;
                    try
                    {
                        using (var fc = new System.Drawing.Text.InstalledFontCollection())
                            foreach (var f in fc.Families)
                                if (f.Name.Equals("Segoe UI Semibold", StringComparison.OrdinalIgnoreCase))
                                {
                                    _familieSemiBold = "Segoe UI Semibold";
                                    break;
                                }
                    }
                    catch { _familieSemiBold = FontFamily; }
                }
                return _familieSemiBold;
            }
        }

        /// <summary>Creează un font din scara aplicației (familia Segoe UI).</summary>
        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(FontFamily, size, style);
        }

        /// <summary>
        /// Creează un font „SemiBold": folosește familia Semibold când există,
        /// altfel Segoe UI Bold (aceeași greutate vizuală pe Win10 fără Semibold).
        /// </summary>
        public static Font SemiBold(float size)
        {
            string familie = FamilieSemiBold;
            return familie == FontFamily
                ? new Font(FontFamily, size, FontStyle.Bold)
                : new Font(familie, size, FontStyle.Regular);
        }

        // ── Scara canonică ──────────────────────────────────────
        /// <summary>Brand în topbar (12pt SemiBold).</summary>
        public static Font FontBrand => SemiBold(12f);

        /// <summary>Titlu de pagină / dashboard (20pt SemiBold).</summary>
        public static Font FontDisplay => SemiBold(20f);

        /// <summary>Titlu de formular (16pt SemiBold).</summary>
        public static Font FontTitle => SemiBold(16f);

        /// <summary>Subtitlu de secțiune (11pt SemiBold).</summary>
        public static Font FontHeading => SemiBold(11f);

        /// <summary>Text curent (9.5pt).</summary>
        public static Font FontBody => Font(9.5f);

        /// <summary>Text curent îngroșat (9.5pt Bold).</summary>
        public static Font FontBodyBold => Font(9.5f, FontStyle.Bold);

        /// <summary>Text de sprijin / subtitlu (9pt).</summary>
        public static Font FontSubtitle => Font(9f);

        /// <summary>Text mic (8.5pt).</summary>
        public static Font FontSmall => Font(8.5f);

        /// <summary>Text mic îngroșat — etichete, unități (8.5pt Bold).</summary>
        public static Font FontSmallBold => Font(8.5f, FontStyle.Bold);

        /// <summary>Meta / hint (8pt).</summary>
        public static Font FontCaption => Font(8f);

        /// <summary>Meta italic (8pt Italic).</summary>
        public static Font FontCaptionItalic => Font(8f, FontStyle.Italic);

        /// <summary>Etichetă uppercase de card KPI (7.5pt Bold).</summary>
        public static Font FontOverline => Font(7.5f, FontStyle.Bold);

        /// <summary>Etichetă de câmp de formular (8.5pt Bold).</summary>
        public static Font FontLabel => Font(8.5f, FontStyle.Bold);

        /// <summary>Text de buton (9pt SemiBold).</summary>
        public static Font FontButton => SemiBold(9f);

        /// <summary>Element de meniu lateral (9.5pt).</summary>
        public static Font FontNav => Font(9.5f);

        /// <summary>Element de meniu lateral activ (9.5pt SemiBold).</summary>
        public static Font FontNavActive => SemiBold(9.5f);

        /// <summary>Valoarea mare a unui card KPI (22pt).</summary>
        public static Font FontKpiValue => Font(22f);

        /// <summary>Unitatea valorii KPI (9pt).</summary>
        public static Font FontKpiUnit => Font(9f);

        /// <summary>Detaliu tehnic monospațiat (8.5pt Consolas).</summary>
        public static Font FontMono => new Font(FontFamilyMono, 8.5f);

        
        //  HELPER-E GRAFICE
        

        
        /// Creează o culoare derivată cu alpha (0-255).
        /// Folosit pentru fundaluri de hover, badge-uri, selecție.
        
        public static Color Alpha(Color culoare, int alpha)
        {
            if (alpha < 0) alpha = 0;
            if (alpha > 255) alpha = 255;
            return Color.FromArgb(alpha, culoare.R, culoare.G, culoare.B);
        }

        
        /// Construiește un GraphicsPath dreptunghi cu colțuri rotunjite.
        /// Dacă radius ≤ 0, întoarce un dreptunghi simplu (evită arc-uri
        /// invalide care arunca ArgumentException la dimensiuni mici).
        
        public static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int d = radius * 2;
            // Limitare defensivă: raza nu poate depăși jumătate din latura mică
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            if (d <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        
        /// Desenează o linie orizontală de 1px pe toată lățimea zonei date
        /// (folosit ca separator între secțiuni și sub topbar).
        /// Culoarea implicită este Design.Divider.
        
        public static void DrawSeparator(Graphics g, int y, int width,
            Color? culoare = null)
        {
            using (var pen = new Pen(culoare ?? Divider, 1f))
                g.DrawLine(pen, 0, y, width, y);
        }

        
        /// Desenează o bară plată de accent (o singură culoare, fără gradient
        /// și fără glow) — pentru liniile de accent și indicatorii de selecție.
        /// 
        public static void DrawAccentBar(Graphics g, Rectangle zona,
            Color culoare)
        {
            using (var brush = new SolidBrush(culoare))
                g.FillRectangle(brush, zona);
        }

        /// <summary>
        /// Fundalul „card": umple cu Design.Surface + contur 1px Border,
        /// colțuri rotunjite la Design.Radius. Folosit de KpiCard,
        /// panouri dashboard, containere de secțiune.
        /// </summary>
        public static void PaintCard(Graphics g, Rectangle rect, int radius = Radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedPath(rect, radius))
            {
                using (var b = new SolidBrush(Surface))
                    g.FillPath(b, path);
                using (var p = new Pen(Border, 1f))
                    g.DrawPath(p, path);
            }
        }

        
        /// Curăță o zonă cu culoarea de fundal a părintelui (rezolvă
        /// fundalul opac al unui control ale cărui colțuri rotunjite
        /// lasă la vedere fundalul formei).
        
        public static Color ResolveOpaqueBackground(Control control, Color fallback)
        {
            Control c = control?.Parent;
            while (c != null)
            {
                if (c.BackColor.A == 255) return c.BackColor;
                c = c.Parent;
            }
            return fallback;
        }
    }
}