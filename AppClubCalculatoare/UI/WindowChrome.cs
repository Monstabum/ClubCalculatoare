using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Chrome de fereastră fără bordură (borderless) pentru AppClubCalculatoare.
    /// Oferă: umbră, colțuri rotunjite (Win11/DWM), redimensionare din marginile
    /// ferestrei, mutare prin bara de titlu și clamp la maximizare (fără acoperirea
    /// taskbar-ului, multi-monitor). Se integrează prin override la
    /// <see cref="Form.CreateParams"/> + <see cref="Form.WndProc(ref Message)"/>.
    /// Fără referințe externe — doar P/Invoke user32/dwmapi.
    /// </summary>
    internal static class WindowChrome
    {
        // ── Mesaje Windows ─────────────────────────────────────────
        public const int WM_NCCALCSIZE    = 0x0083;
        public const int WM_NCHITTEST     = 0x0084;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int WM_GETMINMAXINFO = 0x0024;

        public const int HTCAPTION      = 2;
        public const int HTLEFT         = 10;
        public const int HTRIGHT        = 11;
        public const int HTTOP          = 12;
        public const int HTTOPLEFT      = 13;
        public const int HTTOPRIGHT     = 14;
        public const int HTBOTTOM       = 15;
        public const int HTBOTTOMLEFT   = 16;
        public const int HTBOTTOMRIGHT  = 17;
        public const int HTCLIENT       = 1;

        // Grosimea zonei de prindere pentru redimensionare (px).
        public const int ResizeBorder = 6;

        // Stil de clasă fereastră: umbră moale (Win10). Pe Win11 umbra vine din DWM.
        public const int CS_DROPSHADOW = 0x00020000;

        // ── DWM: colțuri rotunjite stil Windows 11 ─────────────────
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public Point ptReserved;
            public Point ptMaxSize;
            public Point ptMaxPosition;
            public Point ptMinTrackSize;
            public Point ptMaxTrackSize;
        }

        /// <summary>
        /// Atașează evenimentul de tragere (mutare fereastră) pe una sau mai
        /// multe controale-tampon (ex: topBar, eticheta brand-ului, logo).
        /// </summary>
        public static void AttachDrag(Form form, params Control[] handles)
        {
            if (form == null) return;
            foreach (var h in handles)
            {
                if (h == null) continue;
                h.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left) Drag(form);
                };
            }
        }

        /// <summary>Trimite mesajul de „mutare fereastră\" (HTCAPTION) către Win32.</summary>
        public static void Drag(Form form)
        {
            if (form == null || !form.IsHandleCreated) return;
            ReleaseCapture();
            SendMessage(form.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        /// <summary>
        /// Colțuri rotunjite pe Windows 11 (ignorat pe versiuni mai vechi — nu aruncă).
        /// </summary>
        public static void ApplySystemChrome(Form form)
        {
            try
            {
                if (form == null || !form.IsHandleCreated) return;
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref pref, sizeof(int));
            }
            catch
            {
                // Fără DWM: umbra clasică din CreateParams este de ajuns.
            }
        }

        // ── DWM: bară de titlu întunecată (Win10 2004+ / Win11) ─────
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE     = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        /// <summary>
        /// Bară de titlu închisă pentru ferestrele cu ramă standard
        /// (dialogurile), ca să se potrivească cu tema întunecată a
        /// aplicației. Se aplică la crearea handle-ului; ignorat pe
        /// versiunile fără suport DWM.
        /// </summary>
        public static void EnableDarkTitleBar(Form form)
        {
            if (form == null) return;
            if (form.IsHandleCreated) ApplyDarkTitleBar(form);
            else form.HandleCreated += (s, e) => ApplyDarkTitleBar(form);
        }

        private static void ApplyDarkTitleBar(Form form)
        {
            try
            {
                int on = 1;
                int hr = DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref on, sizeof(int));
                if (hr != 0)
                    DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD,
                        ref on, sizeof(int));
            }
            catch
            {
                // Fără suport DWM — rămâne bara de titlu implicită.
            }
        }

        /// <summary>
        /// Procesează mesajele esențiale pentru chrome-ul borderless.
        /// Întoarce true dacă mesajul a fost complet gestionat (apelantul NU mai
        /// apelează base.WndProc).
        /// </summary>
        public static bool ProcessMessage(Form form, ref Message m)
        {
            switch (m.Msg)
            {
                // Clientul ocupă toată fereastra — eliminăm complet rama implicită.
                case WM_NCCALCSIZE:
                    if (m.WParam != IntPtr.Zero) // TRUE
                    {
                        m.Result = IntPtr.Zero;
                        return true;
                    }
                    return false;

                // Zone de redimensionare pe marginile ferestrei.
                case WM_NCHITTEST:
                {
                    Point cl = form.PointToClient(Cursor.Position);
                    int b = ResizeBorder;
                    int w = form.ClientSize.Width;
                    int h = form.ClientSize.Height;

                    bool l = cl.X >= 0 && cl.X < b;
                    bool r = cl.X <= w - 1 && cl.X >= w - b;
                    bool t = cl.Y >= 0 && cl.Y < b;
                    bool bo = cl.Y <= h - 1 && cl.Y >= h - b;

                    IntPtr hit = (IntPtr)HTCLIENT;
                    if (t && l) hit = (IntPtr)HTTOPLEFT;
                    else if (t && r) hit = (IntPtr)HTTOPRIGHT;
                    else if (bo && l) hit = (IntPtr)HTBOTTOMLEFT;
                    else if (bo && r) hit = (IntPtr)HTBOTTOMRIGHT;
                    else if (l) hit = (IntPtr)HTLEFT;
                    else if (r) hit = (IntPtr)HTRIGHT;
                    else if (t) hit = (IntPtr)HTTOP;
                    else if (bo) hit = (IntPtr)HTBOTTOM;

                    m.Result = hit;
                    return true;
                }

                // Limitează fereastra maximizată la zona de lucru (fără taskbar,
                // corect pe multi-monitor, fără „sângerare\" pe margini).
                case WM_GETMINMAXINFO:
                    return ClampMaxInfo(form, ref m);
            }
            return false;
        }

        private static bool ClampMaxInfo(Form form, ref Message m)
        {
            try
            {
                var scr = Screen.FromControl(form);
                var mm = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                mm.ptMaxTrackSize.X = scr.WorkingArea.Width;
                mm.ptMaxTrackSize.Y = scr.WorkingArea.Height;
                mm.ptMaxSize.X = scr.WorkingArea.Width;
                mm.ptMaxSize.Y = scr.WorkingArea.Height;
                mm.ptMaxPosition.X = scr.WorkingArea.X - scr.Bounds.X;
                mm.ptMaxPosition.Y = scr.WorkingArea.Y - scr.Bounds.Y;
                Marshal.StructureToPtr(mm, m.LParam, true);
                m.Result = IntPtr.Zero;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

