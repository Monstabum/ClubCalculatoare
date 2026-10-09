using System;
using System.Drawing;
using System.Windows.Forms;
namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Strat istoric de compatibilitate peste <see cref="MaterialTheme"/>.
    ///
    /// PACHET-1 (BUG-14 complet): valorile suprapuse NU mai sunt duplicate —
    /// fiecare culoare/dimensiune comuna redirectioneaza catre constanta
    /// canonica din MaterialTheme (aceleasi valori ARGB, zero schimbare
    /// vizuala). Doar membrii fara echivalent (TextDim, BorderStrong,
    /// AccentSoft, WarningSoft, Radius*, SidebarW, fonturile H*/Nav*/Kpi*)
    /// raman definiti aici. Codul nou trebuie sa foloseasca direct
    /// MaterialTheme.
    /// </summary>
    internal static class Theme
    {
        // ── Culori canonice (forward catre MaterialTheme, aceleasi valori) ──
        public static Color Background => MaterialTheme.Background;
        public static Color Surface    => MaterialTheme.Surface;
        public static Color Sidebar    => MaterialTheme.SidebarBg;    // #131A29
        public static Color Text       => MaterialTheme.TextPrimary;  // #F1F5F9
        public static Color TextMuted  => MaterialTheme.TextSecondary;// #94A3B8
        public static Color Border     => MaterialTheme.Divider;      // #233046
        public static Color Accent     => MaterialTheme.Primary;      // #7C3AED
        public static Color AccentHover => MaterialTheme.PrimaryDark; // #5B21B6
        public static Color Success    => MaterialTheme.Success;
        public static Color Warning    => MaterialTheme.Warning;
        public static Color Danger     => MaterialTheme.Danger;

        // ── Culori fara echivalent in MaterialTheme (definite o singura data, aici) ──
        // Adaptate pentru tema inchisa: tonuri soft folosite pe fundal petrol.
        public static readonly Color TextDim = Color.FromArgb(0x64, 0x74, 0x8B);
        public static readonly Color BorderStrong = Color.FromArgb(0x33, 0x40, 0x5A);
        public static readonly Color AccentSoft = Color.FromArgb(0x2A, 0x23, 0x50);  // violet soft pe inchis
        public static readonly Color WarningSoft = Color.FromArgb(0x3A, 0x2E, 0x12); // galben soft pe inchis

        public const int Radius = 10;
        public const int RadiusSmall = 6;
        public const int SidebarW = 230;
        public const int TopBarH = MaterialTheme.TopBarH;

        public const string FontFamily = MaterialTheme.FontFamily;
        public static readonly Font H1 = new Font(FontFamily, 18f, FontStyle.Bold);
        public static readonly Font H2 = new Font(FontFamily, 14f, FontStyle.Bold);
        public static readonly Font H3 = new Font(FontFamily, 11.5f, FontStyle.Bold);
        public static readonly Font Body = new Font(FontFamily, 9.5f);
        public static readonly Font BodyBold = new Font(FontFamily, 9.5f, FontStyle.Bold);
        public static readonly Font Small = new Font(FontFamily, 8.5f);
        public static readonly Font SmallBold = new Font(FontFamily, 8.5f, FontStyle.Bold);
        public static readonly Font Caption = new Font(FontFamily, 8f);
        public static readonly Font CaptionItalic = new Font(FontFamily, 8f, FontStyle.Italic);
        public static readonly Font NavFont = new Font(FontFamily, 10f);
        public static readonly Font NavFontActive = new Font(FontFamily, 10f, FontStyle.Bold);
        public static readonly Font KpiValue = new Font(FontFamily, 21f, FontStyle.Bold);
        public static readonly Font KpiUnit = new Font(FontFamily, 9f, FontStyle.Bold);

        // PACHET-1: wrapper peste MaterialTheme.ApplyRounded — o singura implementare.
        public static void Rounded(Control control, int radius)
        {
            MaterialTheme.ApplyRounded(control, radius);
        }

        // PACHET-1: wrapper-e peste implementarile canonice din MaterialTheme
        // (rezolva fundalul opac / amestecul de culori pentru butoanele custom).
        public static Color ResolveOpaqueBackground(Control control, Color fallback)
        {
            return MaterialTheme.ResolveOpaqueBackground(control, fallback);
        }

        public static Color Blend(Color baseColor, Color overlay)
        {
            return MaterialTheme.Blend(baseColor, overlay);
        }

        // FIX_14: wrapper peste MaterialTheme.StyleGrid — păstrăm o singură implementare canonică (MaterialTheme),
        // iar Theme rămâne doar pentru compatibilitate (constantele sale istorice nu au fost șterse ca să nu rupem apelurile existente).
        public static void StyleGrid(DataGridView grid, Color headerColor)
        {
            MaterialTheme.StyleGrid(grid, headerColor);
        }
    }
}
