using System;
using System.Windows.Forms;

namespace AppClubCalculatoare
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ── Handler global pentru exceptii negestionate ──────────
            Application.ThreadException += (s, e) =>
            {
                AppLogger.Error(e.Exception, "Application.ThreadException");
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
                    // FIX_01: логин, не ФИО — Username pentru БД/аудит, NumeComplet (ФИО) pentru UI
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
                            // FIX_01: logăm login-ul real, nu ФИО
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
