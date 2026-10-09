using System;
using System.IO;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Logger simplu cu scriere thread-safe intr-un fisier text.
    /// Folosit pentru erori si evenimente de securitate (login).
    ///
    /// Format linie:
    ///   [2025-01-15 14:32:07] [ERROR] FormCRUD.IncarcaDate: Eroare mesaj...
    /// </summary>
    internal static class AppLogger
    {
        // Lock pentru acces concurent (desi WinForms e single-thread UI)
        private static readonly object _lock = new object();

        private static string LogFilePath => DatabaseConfig.LogFilePath;

        // ── Niveluri de severitate ──────────────────────────────────

        public static void Info(string mesaj, string sursa = null)
            => Scrie("INFO ", sursa, mesaj);

        public static void Warn(string mesaj, string sursa = null)
            => Scrie("WARN ", sursa, mesaj);

        public static void Error(string mesaj, string sursa = null)
            => Scrie("ERROR", sursa, mesaj);

        public static void Error(Exception ex, string sursa = null)
            => Scrie("ERROR", sursa, $"{ex.GetType().Name}: {ex.Message}");

        /// <summary>Logheaza evenimentele de securitate (login, logout).</summary>
        public static void Security(string mesaj, string username = null)
        {
            string text = username != null ? $"[{username}] {mesaj}" : mesaj;
            Scrie("SEC  ", "Auth", text);
        }

        // ── Scriere efectiva ────────────────────────────────────────

        private static void Scrie(string nivel, string sursa, string mesaj)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string linie = $"[{timestamp}] [{nivel}] {sursa ?? "App"}: {mesaj}";

                lock (_lock)
                {
                    // Creeaza directorul daca nu exista
                    string dir = Path.GetDirectoryName(LogFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.AppendAllText(LogFilePath, linie + Environment.NewLine);

                    // Rotatie simpla: daca fisierul depaseste 5 MB, il arhiveaza
                    var fi = new FileInfo(LogFilePath);
                    if (fi.Exists && fi.Length > 5 * 1024 * 1024)
                    {
                        string arhiva = LogFilePath.Replace(".log",
                            $"_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                        File.Move(LogFilePath, arhiva);
                    }
                }
            }
            catch
            {
                // Loggerul nu trebuie sa arunce exceptii catre apelant
            }
        }
    }
}
