using System;
using System.Windows.Forms;

namespace AppClubCalculatoare
{
    internal static class Program
    {
        private static string _ultimaEroareAfisata = "";
        private static DateTime _momentUltimaEroare = DateTime.MinValue;

        [STAThread]
        static void Main()
        {
            // ── DPI: proces „per-monitor v2" ─────────────────────────
            // Fără asta, Windows consideră aplicația DPI-unaware și
            // scalează bitmap toată fereastra la 125%/150% — textul și
            // butoanele apar „mătăhăloase". Trebuie apelat ÎNAINTE de
            // crearea oricărei ferestre. (Vezi și Elementele din UI
            // care își scalează singure desenul: MaterialUI.Scale.)
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // ── FIX BUG-04: cultură românească pentru tot procesul ────
            // Fără asta, MessageBox/OpenFileDialog/PrintDialog preiau limba
            // sistemului de operare: pe un Windows cu altă limbă butoanele
            // apareau în acea limbă (ex. „Да"/"Нет") în mijlocul unei
            // interfețe integral românești. Setăm și Culture (formate de
            // număr/dată și separatorul CSV) și UICulture (textul controalelor).
            try
            {
                var ro = new System.Globalization.CultureInfo("ro-RO");
                System.Globalization.CultureInfo.DefaultThreadCurrentCulture   = ro;
                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = ro;
                System.Threading.Thread.CurrentThread.CurrentCulture   = ro;
                System.Threading.Thread.CurrentThread.CurrentUICulture = ro;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Setare cultură ro-RO eșuată: " + ex.Message, "Program");
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ── Handler global pentru exceptii negestionate ──────────
            // Protectie anti-bucla: aceeasi eroare repetata la <5s distanta
            // (ex. la fiecare repaint al unui control stricat) se logheaza
            // fara dialog, ca aplicatia sa poata fi inchisa normal.
            Application.ThreadException += (s, e) =>
            {
                AppLogger.Error(e.Exception, "Application.ThreadException");
                string cheie = e.Exception.GetType().FullName + "|" + e.Exception.Message;
                var acum = DateTime.Now;
                if (cheie == _ultimaEroareAfisata && (acum - _momentUltimaEroare).TotalSeconds < 5)
                    return;
                _ultimaEroareAfisata = cheie;
                _momentUltimaEroare = acum;
                MessageBox.Show(
                    "A aparut o eroare neasteptata:\n\n" + e.Exception.Message +
                    "\n\nEroarea a fost inregistrata in fisierul de log.",
                    "Eroare critica", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                AppLogger.Error(ex?.Message ?? e.ExceptionObject?.ToString(),
                                "AppDomain.UnhandledException");
            };

            // ── Test conexiune la baza de date ───────────────────────
            if (!TestConexiuneLaBazaDeDate())
                return;

            // ── MODUL_FOTO: foldere foto + verificare integritate ──
            // Nu blocheaza pornirea: orice eroare ajunge doar in log.
            try
            {
                PhotoService.EnsureFolders();
                System.Collections.Generic.List<int> lipsa =
                    PhotoService.VerificaIntegritate();
                if (lipsa.Count > 0)
                    AppLogger.Warn(string.Format(
                        "Verificare foto: {0} inregistrari fara fisier pe disc.",
                        lipsa.Count), "Program");
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Initializare modul foto esuata: " + ex.Message,
                    "Program");
            }

            AppLogger.Info("Aplicatie Club de Calculatoare pornita.", "Program");

            // ── Bucla principala (Login → Main → eventual Logout) ────
            while (true)
            {
                using (var loginForm = new FormLogin())
                {
                    if (loginForm.ShowDialog() != DialogResult.OK || !loginForm.Autentificat)
                        break;

                    // ── Seteaza SessionInfo pentru audit trail ───────
                    // FIX_01: login-ul real, nu numele complet — Username pentru BD/audit, NumeComplet (nume complet) pentru UI
                    SessionInfo.Username    = loginForm.Username;
                    SessionInfo.NumeComplet = loginForm.NumeUtilizator;
                    SessionInfo.Rol         = loginForm.RolUtilizator;

                    using (var mainForm = new FormMainCuLogin(
                        loginForm.RolUtilizator,
                        loginForm.NumeUtilizator))
                    {
                        mainForm.ShowDialog();

                        if (mainForm.DialogResult == DialogResult.Retry)
                        {
                            // FIX_01: logăm login-ul real, nu numele complet
                            AppLogger.Security($"Deconectare: {loginForm.Username}");
                            SessionInfo.Reset();
                            continue;
                        }
                    }
                }

                break;
            }

            AppLogger.Info("Aplicatie Club de Calculatoare inchisa.", "Program");
            Application.Exit();
        }

        private static bool TestConexiuneLaBazaDeDate()
        {
            try
            {
                _ = DatabaseConfig.ConnectionString;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    ex.Message + "\n\nVerifica fisierul App.config si reporneste aplicatia.",
                    "Configuratie lipsa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!DatabaseConfig.TestConnection(out string eroare))
            {
                MessageBox.Show(
                    "Nu s-a putut conecta la baza de date.\n\n" +
                    "Verifica:\n" +
                    "  1. SQL Server este pornit\n" +
                    "  2. Baza de date 'ClubCalculatoare' exista\n" +
                    "  3. Connection string-ul din App.config este corect\n\n" +
                    "Detaliu tehnic: " + eroare,
                    "Eroare conexiune", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }
    }
}
