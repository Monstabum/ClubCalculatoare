using System;
using System.Drawing;
using System.Windows.Forms;
namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Strat de compatibilitate peste <see cref="MaterialTheme"/> (alias pur).
    ///
    /// Toate valorile (culori, dimensiuni, fonturi) sunt acum definite o singură
    /// dată în <see cref="MaterialTheme"/>; aici se face doar redirecționare, cu
    /// aceleași valori ARGB — zero schimbare vizuală. Codul nou trebuie să
    /// folosească direct MaterialTheme.
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
        public static Color TextDim     => MaterialTheme.TextDim;
        public static Color BorderStrong => MaterialTheme.BorderStrong;
        public static Color AccentSoft   => MaterialTheme.AccentSoft;
        public static Color WarningSoft  => MaterialTheme.WarningSoft;

        public const int Radius = MaterialTheme.ButtonRadius;
        public const int RadiusSmall = 6;
        public const int SidebarW = MaterialTheme.SidebarW;
        public const int TopBarH = MaterialTheme.TopBarH;

        public const string FontFamily = MaterialTheme.FontFamily;
        public static Font H1             => MaterialTheme.H1;
        public static Font H2             => MaterialTheme.H2;
        public static Font H3             => MaterialTheme.H3;
        public static Font Body           => MaterialTheme.Body;
        public static Font BodyBold       => MaterialTheme.BodyBold;
        public static Font Small          => MaterialTheme.Small;
        public static Font SmallBold      => MaterialTheme.SmallBold;
        public static Font Caption        => MaterialTheme.Caption;
        public static Font CaptionItalic  => MaterialTheme.CaptionItalic;
        public static Font NavFont        => MaterialTheme.NavFont;
        public static Font NavFontActive  => MaterialTheme.NavFontActive;
        public static Font KpiValue       => MaterialTheme.KpiValue;
        public static Font KpiUnit        => MaterialTheme.KpiUnit;

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
