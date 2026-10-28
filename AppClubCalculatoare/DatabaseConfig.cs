using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Punct central pentru configurarea bazei de date.
    /// Citeste connection string-ul din App.config, eliminand
    /// orice valoare hardcodata din cod.
    /// </summary>
    internal static class DatabaseConfig
    {
        private const string ConnectionName = "ClubCalculatoare";

        // ── Echipament principala ──────────────────────────────────
        // Lazy-load: se citeste o singura data si se pastreaza in cache.
        private static string _connectionString;

        /// <summary>
        /// Returneaza connection string-ul configurat in App.config.
        /// Arunca InvalidOperationException daca lipseste configuratia.
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                if (_connectionString != null)
                    return _connectionString;

                var entry = ConfigurationManager.ConnectionStrings[ConnectionName];

                if (entry == null || string.IsNullOrWhiteSpace(entry.ConnectionString))
                    throw new InvalidOperationException(
                        $"Connection string-ul '{ConnectionName}' lipseste din App.config.\n" +
                        "Verifica sectiunea <connectionStrings>.");

                _connectionString = DbSettings.Build(entry.ConnectionString);
                return _connectionString;
            }
        }

        // ── Setari aplicatie ────────────────────────────────────────

        /// <summary>Numarul maxim de tentative esuate de login (0 = dezactivat).</summary>
        public static int MaxLoginAttempts
        {
            get
            {
                string val = ConfigurationManager.AppSettings["MaxLoginAttempts"];
                return int.TryParse(val, out int n) ? n : 5;
            }
        }

        /// <summary>Durata blocarii contului dupa tentativale esuate (minute).</summary>
        public static int LoginLockoutMinutes
        {
            get
            {
                string val = ConfigurationManager.AppSettings["LoginLockoutMinutes"];
                return int.TryParse(val, out int n) && n > 0 ? n : 15;
            }
        }

        /// <summary>
        /// Expirarea sesiunii la inactivitate (minute; 0 = dezactivat).
        /// Valorile negative sunt tratate tot ca dezactivat (vezi CreeazaTimerInactivitate).
        /// </summary>
        public static int SessionTimeoutMinutes
        {
            get
            {
                string val = ConfigurationManager.AppSettings["SessionTimeoutMinutes"];
                // FIX B3: 0 (sau negativ) = dezactivat, nu se mai inlocuieste cu 15.
                return int.TryParse(val, out int n) ? n : 15;
            }
        }

        /// <summary>Calea fisierului de log (suporta variabile de mediu).</summary>
        public static string LogFilePath
        {
            get
            {
                string raw = ConfigurationManager.AppSettings["LogFilePath"]
                             ?? @"%AppData%\AppClubCalculatoare\app.log";
                return Environment.ExpandEnvironmentVariables(raw);
            }
        }

        // ── Setari modul foto (MODUL_FOTO) ────────────────────────

        /// <summary>
        /// Radacina folderelor foto (orig / thumb / medium per echipament).
        /// Suporta variabile de mediu (%AppData%).
        /// </summary>
        public static string PhotosRootPath
        {
            get
            {
                string raw = ConfigurationManager.AppSettings["PhotosRootPath"]
                             ?? @"%AppData%\AppClubCalculatoare\Poze";
                return Environment.ExpandEnvironmentVariables(raw);
            }
        }

        /// <summary>Dimensiunea maxima a unui fisier foto (MB).</summary>
        public static int PhotoMaxSizeMB
        {
            get
            {
                string val = ConfigurationManager.AppSettings["PhotoMaxSizeMB"];
                return int.TryParse(val, out int n) && n > 0 ? n : 10;
            }
        }

        /// <summary>Latura miniaturii patrate (px).</summary>
        public static int PhotoThumbSize
        {
            get
            {
                string val = ConfigurationManager.AppSettings["PhotoThumbSize"];
                return int.TryParse(val, out int n) && n > 0 ? n : 200;
            }
        }

        /// <summary>Latura lunga maxima a versiunii medii (px).</summary>
        public static int PhotoMediumSize
        {
            get
            {
                string val = ConfigurationManager.AppSettings["PhotoMediumSize"];
                return int.TryParse(val, out int n) && n > 0 ? n : 1200;
            }
        }

        // ── Metoda de test conexiune ────────────────────────────────

        /// <summary>
        /// Verifica rapid daca serverul SQL este accesibil.
        /// Utila la pornirea aplicatiei pentru a oferi un mesaj clar.
        /// </summary>
        public static bool TestConnection(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Varianta async a testului de conexiune, folosită de ecranele care
        /// nu trebuie să blocheze UI-thread-ul.
        /// </summary>
        public static async Task<(bool Ok, string Error)> TestConnectionAsync(
            CancellationToken ct = default)
        {
            try
            {
                await using (var conn = SqlDb.CreateConnection())
                    await conn.OpenAsync(ct).ConfigureAwait(false);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
