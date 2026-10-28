using System;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Rezultatul unei tentative de autentificare.
    /// </summary>
    internal class AuthResult
    {
        public bool Succes          { get; set; }
        public string Rol           { get; set; }   // "Admin" | "User"
        public string NumeComplet   { get; set; }
        public string MesajEroare   { get; set; }

        /// <summary>
        /// FIX BUG-08: true când contul este blocat temporar (protecție brute-force).
        /// Formularul de login are nevoie de asta ca să afișeze momentul exact al
        /// deblocării și să nu inducă în eroare contorul local de încercări.
        /// </summary>
        public bool Blocat { get; set; }

        /// <summary>Momentul de la care contul poate fi folosit din nou.</summary>
        public DateTime? BlocatPanaLa { get; set; }

        public static AuthResult Esuat(string mesaj) =>
            new AuthResult { Succes = false, MesajEroare = mesaj };

        /// <summary>Eșec din cauza blocării temporare a contului.</summary>
        public static AuthResult BlocatTemporar(string mesaj, DateTime? panaLa) =>
            new AuthResult { Succes = false, MesajEroare = mesaj, Blocat = true, BlocatPanaLa = panaLa };

        public static AuthResult Reusit(string rol, string numeComplet) =>
            new AuthResult { Succes = true, Rol = rol, NumeComplet = numeComplet };
    }

    /// <summary>
    /// Serviciu de autentificare bazat pe tabelul [Utilizatori].
    ///
    /// Fluxul de autentificare:
    ///   1. Cauta utilizatorul dupa username (case-insensitive).
    ///   2. Verifica daca contul este activ (activ = 1).
    ///   3. Calculeaza SHA-256 al parolei introduse.
    ///   4. Compara hash-ul cu cel stocat in baza de date.
    ///   5. La succes, actualizeaza ultima_autentificare.
    ///
    /// SECURITATE:
    ///   - Parola nu este niciodata stocata in clar.
    ///   - Toate interogarile folosesc parametri (anti SQL injection).
    ///   - Mesajele de eroare sunt intentionat vagi (nu reveleaza
    ///     daca username-ul exista sau nu).
    /// </summary>
    internal static class AuthService
    {
        // ── Hash SHA-256 ────────────────────────────────────────────

        /// <summary>
        /// Calculeaza SHA-256 al unui string si returneaza hex lowercase.
        /// Format compatibil cu HASHBYTES('SHA2_256', ...) din SQL Server.
        /// </summary>
        public static string HashParola(string parola)
        {
            if (parola == null) throw new ArgumentNullException(nameof(parola));

            using (var sha = SHA256.Create())
            {
                // FIX: UTF8 explicit, independent de locale-ul Windows.
                // Encoding.Default depinde de codepage-ul ANSI al OS-ului
                // (RO=1250, RU=1251) si produce hash-uri diferite pe masini
                // diferite pentru parole non-ASCII. Pentru parolele ASCII
                // curente (121212/575757) rezultatul e identic cu cel din BD.
                byte[] bytes = Encoding.UTF8.GetBytes(parola);
                byte[] hash  = sha.ComputeHash(bytes);

                // Conversia in hex lowercase — identic cu SQL Server "style 2"
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));

                return sb.ToString();
            }
        }

        // ── Autentificare async ─────────────────────────────────────

        /// <summary>
        /// Verifica credentialele fata de baza de date (async, non-blocking UI).
        /// </summary>
        public static async Task<AuthResult> AutentificaAsync(
            string username, string parola, CancellationToken ct = default)
        {
            // Validare de baza (fara a accesa DB)
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(parola))
                return AuthResult.Esuat("Completeaza atat utilizatorul cat si parola.");

            if (username.Length > 50)
                return AuthResult.Esuat("Username prea lung.");

            string hashIntrodus = HashParola(parola);

            try
            {
                return await SqlDb.WithConnectionAsync(async (conn, token) =>
                {
                    // ── Pasul 1: Preia datele utilizatorului ─────────
                    // Interogare cu parametru → protejat contra SQL injection.
                    // Nu comparam parola direct in SQL — hash-ul e calculat in C#
                    // pentru a nu expune logica de hashing in query.
                    // tentative_esuate / blocat_pana_la: blocare persistenta
                    // (coloanele pot lipsi pe baze vechi — tratate fail-safe).
                    const string query = @"
                        SELECT parola_hash, rol, nume_complet, activ,
                               tentative_esuate, blocat_pana_la
                        FROM   Utilizatori
                        WHERE  username = @username";  // case-insensitive implicit in SQL Server (collation CI)

                    string hashStored, rol, numeComplet;
                    bool activ;
                    int tentative;
                    DateTime? blocatPana;

                    await using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = SqlDb.DefaultTimeoutSeconds;
                        cmd.Parameters.Add("@username", System.Data.SqlDbType.NVarChar, 50)
                                      .Value = username;

                        await using (SqlDataReader reader =
                            await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            if (!await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                // Username inexistent — mesaj intentionat vag
                                AppLogger.Security($"Login esuat: username necunoscut '{username}'");
                                return AuthResult.Esuat("Utilizator sau parola incorecta.");
                            }

                            hashStored  = reader["parola_hash"].ToString();
                            rol         = reader["rol"].ToString();          // "Admin" | "User"
                            numeComplet = reader["nume_complet"].ToString();
                            activ       = Convert.ToBoolean(reader["activ"]);

                            // Blocare persistenta (pot lipsi pe baze vechi — fail-safe)
                            tentative   = ColoanaExista(reader, "tentative_esuate")
                                               ? Convert.ToInt32(reader["tentative_esuate"]) : 0;
                            blocatPana  = ColoanaExista(reader, "blocat_pana_la")
                                               && reader["blocat_pana_la"] != DBNull.Value
                                               ? (DateTime?)Convert.ToDateTime(reader["blocat_pana_la"])
                                               : null;
                        }
                    }

                    // ── Pasul 2: Verifica daca contul e activ ────
                    if (!activ)
                    {
                        AppLogger.Security($"Login esuat: cont inactiv '{username}'");
                        return AuthResult.Esuat("Contul tau este dezactivat. Contacteaza administratorul.");
                    }

                    // ── Pasul 2b: Blocare persistenta brut-force ──
                    // Contorul si termenul de blocare sunt salvate in BD,
                    // deci blocarea supravietuieste repornirii aplicatiei.
                    if (blocatPana.HasValue && blocatPana.Value > DateTime.Now)
                    {
                        int minuteRamase = (int)Math.Ceiling(
                            (blocatPana.Value - DateTime.Now).TotalMinutes);
                        AppLogger.Security(
                            $"Login esuat: cont blocat temporar '{username}'");
                        // FIX BUG-08: mesaj cu momentul exact al deblocării.
                        // Înainte clientul afișa „Repornește aplicația" — sfat
                        // greșit, pentru că blocarea e persistentă în BD.
                        return AuthResult.BlocatTemporar(
                            "Cont blocat din cauza tentativelor esuate.\n" +
                            $"Reia autentificarea dupa {minuteRamase} minut(e) " +
                            $"(dupa ora {blocatPana.Value:HH:mm}).",
                            blocatPana.Value);
                    }

                    // ── Pasul 3: Compara hash-urile ──────────────
                    // Comparatie constanta in timp (evita timing attacks)
                    bool parolaCorespondenta = StringCompareSecure(hashIntrodus, hashStored);

                    if (!parolaCorespondenta)
                    {
                        int maxTentative = DatabaseConfig.MaxLoginAttempts;
                        tentative++;

                        if (maxTentative > 0 && tentative >= maxTentative)
                        {
                            // Epuizare tentative → blocheaza contul in BD
                            DateTime? panaLa = await InregistreazaBlocareAsync(
                                username, tentative, token).ConfigureAwait(false);
                            AppLogger.Security(
                                $"Cont blocat persistent: '{username}' dupa {tentative} tentative");
                            // FIX BUG-08: spunem exact până când e blocat contul,
                            // în loc să trimitem utilizatorul să repornească aplicația.
                            return AuthResult.BlocatTemporar(
                                $"Cont blocat pentru {DatabaseConfig.LoginLockoutMinutes} minute\n" +
                                "din cauza tentativelor esuate repetate." +
                                (panaLa.HasValue ? $"\nReia autentificarea dupa ora {panaLa.Value:HH:mm}." : ""),
                                panaLa);
                        }

                        await InregistreazaTentativaAsync(username, tentative, token)
                            .ConfigureAwait(false);
                        AppLogger.Security(
                            $"Login esuat: parola gresita pentru '{username}' ({tentative}/{maxTentative})");
                        return AuthResult.Esuat("Utilizator sau parola incorecta.");
                    }

                    // ── Pasul 3b: Parola corecta → reseteaza contorul ─
                    if (tentative > 0 || blocatPana.HasValue)
                        await ReseteazaBlocareAsync(username, token).ConfigureAwait(false);

                    // ── Pasul 4: Actualizeaza ultima_autentificare ─
                    await ActualizeazaUltimaAutentificareAsync(conn, username, token)
                        .ConfigureAwait(false);

                    AppLogger.Security($"Login reusit: '{username}' | Rol: {rol}", username);
                    return AuthResult.Reusit(rol, numeComplet);
                }, ct).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                AppLogger.Error(ex, "AuthService.AutentificaAsync");
                // Nu expunem detalii tehnice utilizatorului
                return AuthResult.Esuat("Eroare de conectare la baza de date. Incearca din nou.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AuthService.AutentificaAsync");
                return AuthResult.Esuat("Eroare neasteptata. Contacteaza administratorul.");
            }
        }

        // FIX_15: varianta sincronă Autentifica(...) a fost eliminată — era sync-over-async
        // cu risc de deadlock; toate apelurile folosesc AutentificaAsync.

        // ── Helpers blocare persistenta (brut-force protection) ─────

        private static bool ColoanaExista(SqlDataReader reader, string coloana)
        {
            for (int i = 0; i < reader.FieldCount; i++)
                if (reader.GetName(i) == coloana) return true;
            return false;
        }

        /// <summary>Creste contorul de tentative esuate in BD.</summary>
        private static async Task InregistreazaTentativaAsync(
            string username, int tentative, CancellationToken ct)
        {
            try
            {
                const string sql = @"
                    UPDATE Utilizatori SET tentative_esuate = @t
                    WHERE  username = @user";
                await SqlDb.ExecuteAsync(sql, p =>
                {
                    p.Add("@t",    System.Data.SqlDbType.Int).Value           = tentative;
                    p.Add("@user", System.Data.SqlDbType.NVarChar, 50).Value  = username;
                }, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Coloana poate lipsi pe baze vechi — blocarea degradata e acceptabila
                AppLogger.Warn($"Nu s-a putut inregistra tentativa: {ex.Message}", "AuthService");
            }
        }

        /// <summary>
        /// Blocheaza contul in BD pana la NOW + LoginLockoutMinutes.
        /// FIX BUG-08: intoarce momentul exact al deblocarii (calculat de SQL Server,
        /// nu de client — astfel mesajul afisat corespunde chiar cu ce e in BD).
        /// </summary>
        private static async Task<DateTime?> InregistreazaBlocareAsync(
            string username, int tentative, CancellationToken ct)
        {
            try
            {
                const string sql = @"
                    UPDATE Utilizatori
                    SET    tentative_esuate = @t,
                           blocat_pana_la   = DATEADD(MINUTE, @minute, GETDATE())
                    OUTPUT INSERTED.blocat_pana_la
                    WHERE  username = @user";

                object rezultat = await SqlDb.ScalarAsync<object>(sql, p =>
                {
                    p.Add("@t",      System.Data.SqlDbType.Int).Value           = tentative;
                    p.Add("@minute", System.Data.SqlDbType.Int).Value           = DatabaseConfig.LoginLockoutMinutes;
                    p.Add("@user",   System.Data.SqlDbType.NVarChar, 50).Value  = username;
                }, ct, SqlDb.DefaultTimeoutSeconds, retryTransient: false).ConfigureAwait(false);

                if (rezultat != null && rezultat != DBNull.Value)
                    return Convert.ToDateTime(rezultat);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Nu s-a putut bloca contul in BD: {ex.Message}", "AuthService");
            }
            return null;
        }

        /// <summary>Reseteaza contorul si blocarea dupa un login reusit.</summary>
        private static async Task ReseteazaBlocareAsync(string username, CancellationToken ct)
        {
            try
            {
                const string sql = @"
                    UPDATE Utilizatori
                    SET    tentative_esuate = 0, blocat_pana_la = NULL
                    WHERE  username = @user";
                await SqlDb.ExecuteAsync(sql, p =>
                    p.Add("@user", System.Data.SqlDbType.NVarChar, 50).Value = username,
                    ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Nu s-a putut reseta blocarea: {ex.Message}", "AuthService");
            }
        }

        /// <summary>
        /// FIX BUG-08: citeste din BD momentul pana la care contul este blocat.
        /// Folosit de formularul de login ca sa arate imediat, la pornire, starea
        /// reala a contului (blocarea e persistenta, deci supravietuieste
        /// repornirii aplicatiei). Intoarce null daca nu e blocat sau daca
        /// informatia nu poate fi citita.
        /// </summary>
        public static async Task<DateTime?> ObtineBlocarePanaLaAsync(
            string username, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            try
            {
                const string sql = @"
                    SELECT blocat_pana_la
                    FROM   Utilizatori
                    WHERE  username = @u";

                object val = await SqlDb.ScalarAsync<object>(sql,
                    p => p.Add("@u", System.Data.SqlDbType.NVarChar, 50).Value = username,
                    ct).ConfigureAwait(false);

                if (val == null || val == DBNull.Value) return null;
                DateTime pana = Convert.ToDateTime(val);
                return pana > DateTime.Now ? (DateTime?)pana : null;
            }
            catch (Exception ex)
            {
                // Coloanele pot lipsi pe baze vechi — nu blocam autentificarea.
                AppLogger.Warn($"Nu s-a putut citi starea de blocare: {ex.Message}", "AuthService");
                return null;
            }
        }

        // ── Helpers private ─────────────────────────────────────────

        private static async Task ActualizeazaUltimaAutentificareAsync(
            SqlConnection conn, string username, CancellationToken ct)
        {
            try
            {
                const string sql = @"
                    UPDATE Utilizatori
                    SET    ultima_autentificare = GETDATE()
                    WHERE  username = @username";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = SqlDb.DefaultTimeoutSeconds;
                    cmd.Parameters.Add("@username", System.Data.SqlDbType.NVarChar, 50)
                                  .Value = username;
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Non-critica — nu oprim login-ul daca aceasta actualizare esueaza
                AppLogger.Warn($"Nu s-a putut actualiza ultima_autentificare: {ex.Message}",
                               "AuthService");
            }
        }

        /// <summary>
        /// Comparatie string in timp constant — previne timing attacks.
        /// Returneaza true doar daca ambele string-uri sunt identice.
        /// </summary>
        private static bool StringCompareSecure(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];   // XOR: 0 doar daca toti biti sunt egali

            return result == 0;
        }
    }
}
