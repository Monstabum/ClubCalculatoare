using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Punctele de acces (functiile) protejate de sistemul de permisiuni.
    ///
    /// Sistemul este EXTENSIBIL: un punct nou se adauga doar aici + un INSERT
    /// in tabela Permisiuni din BD, fara alte modificari de cod.
    ///
    /// Niveluri de permisiune:
    ///   MENIU_X   — vizualizarea listei (accesul de baza la punctul X)
    ///   MODIFICARE_X — adaugare + modificare inregistrari in punctul X
    ///   STERGE_X     — stergerea inregistrarilor din punctul X
    ///   PANOU_DASHBOARD — vizualizarea tabloului de bord (KPI)
    ///   GESTIONARE_UTILIZATORI — administrarea conturilor si a drepturilor
    ///
    /// Adminul are automat acces la toate.
    /// </summary>
    internal static class PuncteAcces
    {
        // ── Vizualizare (acces de baza la meniuri) ──────────────────
        public const string MeniuEchipamente = "MENIU_ECHIPAMENTE";
        public const string MeniuSali     = "MENIU_SALI";
        public const string MeniuMembri     = "MENIU_MEMBRI";
        public const string MeniuSesiuni  = "MENIU_SESIUNI";
        public const string MeniuRezervari   = "MENIU_REZERVARI";
        public const string MeniuOperatori      = "MENIU_OPERATORI";
        public const string MeniuRapoarte    = "MENIU_RAPOARTE";

        // ── Modificare (adaugare + editare) ─────────────────────────
        public const string ModificareEchipamente = "MODIFICARE_ECHIPAMENTE";
        public const string ModificareSali     = "MODIFICARE_SALI";
        public const string ModificareMembri     = "MODIFICARE_MEMBRI";
        public const string ModificareSesiuni  = "MODIFICARE_SESIUNI";
        public const string ModificareRezervari   = "MODIFICARE_REZERVARI";
        public const string ModificareOperatori      = "MODIFICARE_OPERATORI";

        // ── Stergere ────────────────────────────────────────────────
        public const string StergeEchipamente = "STERGE_ECHIPAMENTE";
        public const string StergeSali     = "STERGE_SALI";
        public const string StergeMembri     = "STERGE_MEMBRI";
        public const string StergeSesiuni  = "STERGE_SESIUNI";
        public const string StergeRezervari   = "STERGE_REZERVARI";
        public const string StergeOperatori      = "STERGE_OPERATORI";

        // ── Functii transversale ────────────────────────────────────
        public const string PanouDashboard       = "PANOU_DASHBOARD";
        public const string GestionareUtilizatori = "GESTIONARE_UTILIZATORI";

        // ── Fotografii echipamente (MODUL_FOTO) ───────────────────
        // FOTO_VIZUALIZARE_ECHIPAMENTE — cine vede pozele (implicit:
        // oricine are MENIU_ECHIPAMENTE, vezi PhotoService).
        // FOTO_MODIFICARE_ECHIPAMENTE — cine adauga/sterge poze
        // (implicit: oricine are MODIFICARE_ECHIPAMENTE).
        public const string FotoVizualizareEchipamente = "FOTO_VIZUALIZARE_ECHIPAMENTE";
        public const string FotoModificareEchipamente  = "FOTO_MODIFICARE_ECHIPAMENTE";

        // ── Echipamente favorite (per utilizator) ───────────────
        // FAVORITE — cine poate vedea/adauga/sterge echipamentele
        // favorite in lista proprie (tabela Favorite).
        public const string Favorite = "FAVORITE";

        // ── Calculator cost sesiune/abonament ─────────────────────────
        // CALCULATOR_COST — accesul la calculatorul de cost (cost orar, durată, pachet, reduceri).
        public const string CalculatorCost = "CALCULATOR_COST";

        /// <summary>
        /// Catalogul complet pentru interfata de administrare:
        /// (cod, descriere) in ordinea de afisare, grupat pe nivel.
        /// </summary>
        public static readonly KeyValuePair<string, string>[] Toti = new[]
        {
            // Vizualizare
            new KeyValuePair<string, string>(MeniuEchipamente,      "Echipamente — vizualizare"),
            new KeyValuePair<string, string>(MeniuSali,          "Sali — vizualizare"),
            new KeyValuePair<string, string>(MeniuMembri,          "Membri — vizualizare"),
            new KeyValuePair<string, string>(MeniuSesiuni,       "Sesiuni — vizualizare"),
            new KeyValuePair<string, string>(MeniuRezervari,        "Rezervari — vizualizare"),
            new KeyValuePair<string, string>(MeniuOperatori,           "Operatori — vizualizare"),
            new KeyValuePair<string, string>(MeniuRapoarte,         "Rapoarte si Statistici"),
            new KeyValuePair<string, string>(PanouDashboard,        "Tablou de bord (KPI)"),
            // Modificare
            new KeyValuePair<string, string>(ModificareEchipamente, "Echipamente — adaugare/modificare"),
            new KeyValuePair<string, string>(ModificareSali,     "Sali — adaugare/modificare"),
            new KeyValuePair<string, string>(ModificareMembri,     "Membri — adaugare/modificare"),
            new KeyValuePair<string, string>(ModificareSesiuni,  "Sesiuni — adaugare/modificare"),
            new KeyValuePair<string, string>(ModificareRezervari,   "Rezervari — adaugare/modificare"),
            new KeyValuePair<string, string>(ModificareOperatori,      "Operatori — adaugare/modificare"),
            // Stergere
            new KeyValuePair<string, string>(StergeEchipamente,     "Echipamente — stergere"),
            new KeyValuePair<string, string>(StergeSali,         "Sali — stergere"),
            new KeyValuePair<string, string>(StergeMembri,         "Membri — stergere"),
            new KeyValuePair<string, string>(StergeSesiuni,      "Sesiuni — stergere"),
            new KeyValuePair<string, string>(StergeRezervari,       "Rezervari — stergere"),
            new KeyValuePair<string, string>(StergeOperatori,          "Operatori — stergere"),
            // Transversal
            new KeyValuePair<string, string>(GestionareUtilizatori, "Gestionare Utilizatori"),
            // Fotografii echipamente (MODUL_FOTO)
            new KeyValuePair<string, string>(FotoVizualizareEchipamente, "Fotografii echipamente — vizualizare"),
            new KeyValuePair<string, string>(FotoModificareEchipamente, "Fotografii echipamente — adaugare/stergere"),
            // Echipamente favorite (per utilizator)
            new KeyValuePair<string, string>(Favorite, "Echipamente favorite"),
            // Calculator cost sesiune/abonament
            new KeyValuePair<string, string>(CalculatorCost, "Calculator cost sesiune/abonament")
        };

        // ── Mapari helper (tabela CRUD → coduri de permisiune) ──────

        /// <summary>Codul de VIZUALIZARE pentru o tabela CRUD (null = neprotejat).</summary>
        public static string CodMeniu(string tabela)
        {
            switch (tabela)
            {
                case "Echipamente": return MeniuEchipamente;
                case "Sali":     return MeniuSali;
                case "Membri":     return MeniuMembri;
                case "Sesiuni":  return MeniuSesiuni;
                case "Rezervari":   return MeniuRezervari;
                case "Operatori":      return MeniuOperatori;
                default:            return null;
            }
        }

        /// <summary>Codul de MODIFICARE (adaugare/editare) pentru o tabela CRUD.</summary>
        public static string CodModificare(string tabela)
        {
            switch (tabela)
            {
                case "Echipamente": return ModificareEchipamente;
                case "Sali":     return ModificareSali;
                case "Membri":     return ModificareMembri;
                case "Sesiuni":  return ModificareSesiuni;
                case "Rezervari":   return ModificareRezervari;
                case "Operatori":      return ModificareOperatori;
                default:            return null;
            }
        }

        /// <summary>Codul de STERGERE pentru o tabela CRUD.</summary>
        public static string CodStergere(string tabela)
        {
            switch (tabela)
            {
                case "Echipamente": return StergeEchipamente;
                case "Sali":     return StergeSali;
                case "Membri":     return StergeMembri;
                case "Sesiuni":  return StergeSesiuni;
                case "Rezervari":   return StergeRezervari;
                case "Operatori":      return StergeOperatori;
                default:            return null;
            }
        }
    }

    /// <summary>
    /// Serviciu de verificare a drepturilor de acces.
    ///
    /// ARHITECTURA (verificare pe doua niveluri, conform cerintei):
    ///   Nivel 1 — interfata: butoanele pentru care utilizatorul nu are
    ///             drept sunt ascunse/dezactivate in formulare.
    ///   Nivel 2 — backend:  deschiderea ferestrelor si actiunile de scriere
    ///             reverifica permisiunea (inclusiv direct in BD), deci un
    ///             acces ocolind meniul este tot blocat si logat.
    ///
    /// Performanta: permisiunile se incarca la autentificare in SessionInfo;
    /// VerificaAre adauga a doua verificare direct in BD, cu cache scurt.
    /// </summary>
    internal static class PermissionService
    {
        private static readonly object _lock = new object();
        private static Dictionary<string, bool> _cache;
        private static DateTime _cacheTimp = DateTime.MinValue;
        private const int CacheSecunde = 15;

        // ── Incarcare permisiuni la autentificare ───────────────────

        /// <summary>
        /// Incarca permisiunile utilizatorului in SessionInfo.
        /// Se apeleaza in FormLogin dupa autentificare si dupa orice
        /// modificare de drepturi asupra utilizatorului curent.
        /// Adminul primeste setul complet. Fail-safe: la eroare, un
        /// non-admin nu primeste nicio permisiune suplimentara.
        /// </summary>
        public static void IncarcaPermisiuni(string username, bool esteAdmin)
        {
            SessionInfo.Permisiuni.Clear();
            // FIX: cache-ul backend (VerificaAre, 15s) apartinea sesiunii
            // anterioare — la switch de user fara restart, noul utilizator
            // mostenea pana la 15s permisiunile celui vechi.
            InvalideazaCache();

            if (esteAdmin)
            {
                foreach (var p in PuncteAcces.Toti)
                    SessionInfo.Permisiuni.Add(p.Key);
                return;
            }

            try
            {
                const string sql = @"
                    SELECT up.cod_permisiune
                    FROM   UtilizatoriPermisiuni up
                    JOIN   Utilizatori u ON u.id_utilizator = up.id_utilizator
                    WHERE  u.username = @username";

                using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@username", System.Data.SqlDbType.NVarChar, 50)
                                  .Value = username;
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            SessionInfo.Permisiuni.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PermissionService.IncarcaPermisiuni");
            }
        }

        // ── Verificare backend (cu cache scurt) ─────────────────────

        /// <summary>
        /// Verificare "de server": reciteste din BD permisiunea ceruta.
        /// Blocanta pentru orice actiune la care utilizatorul nu are drept,
        /// inclusiv daca fereastra a fost construita programatic.
        /// </summary>
        public static bool VerificaAre(string cod, bool esteAdmin)
        {
            if (esteAdmin) return true;
            // FIX_06: fail-closed — cod null/necunoscut NU deschide accesul.
            if (cod == null) return false;

            lock (_lock)
            {
                if (_cache == null ||
                    (DateTime.Now - _cacheTimp).TotalSeconds > CacheSecunde)
                {
                    _cache = CitesteToateDinBd();
                    _cacheTimp = DateTime.Now;
                }
                return _cache.TryGetValue(cod, out bool ok) && ok;
            }
        }

        /// <summary>
        /// Verificare rapida (UI) pe setul incarcat in sesiune.
        /// NU inlocuieste VerificaAre pentru actiuni sensibile.
        /// Fail-closed: cod null/necunoscut => false.
        /// </summary>
        public static bool AreUI(string cod)
            => cod != null && SessionInfo.ArePermisiune(cod);

        private static Dictionary<string, bool> CitesteToateDinBd()
        {
            var rezultat = new Dictionary<string, bool>();
            try
            {
                const string sql = @"
                    SELECT p.cod, CASE WHEN up.id_utilizator IS NOT NULL
                                       THEN 1 ELSE 0 END AS acordata
                    FROM   Permisiuni p
                    JOIN   Utilizatori u ON u.username = @username
                    LEFT   JOIN UtilizatoriPermisiuni up
                           ON up.cod_permisiune = p.cod
                          AND up.id_utilizator  = u.id_utilizator";

                using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@username", System.Data.SqlDbType.NVarChar, 50)
                                  .Value = SessionInfo.Username;
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            rezultat[reader.GetString(0)] =
                                Convert.ToInt32(reader["acordata"]) == 1;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PermissionService.CitesteToateDinBd");
            }
            return rezultat;
        }

        /// <summary>Invalideaza cache-ul dupa orice modificare de drepturi.</summary>
        public static void InvalideazaCache()
        {
            lock (_lock) { _cache = null; }
        }

        // ── Audit dedicat drepturilor ───────────────────────────────

        /// <summary>
        /// Inregistreaza in AuditLog o actiune legata de sistemul de drepturi
        /// (acordare/revocare permisiuni, schimbare rol, acces blocat).
        /// Tabela = 'Drepturi', actiunea este inclusa in detalii.
        /// </summary>
        public static void AuditDrepturi(string actiune, string detalii)
        {
            try
            {
                const string sql = @"
                    INSERT INTO AuditLog (tabela, id_inregistrare, actiune,
                                          username, data_ora, detalii)
                    VALUES ('Drepturi', 0, @actiune, @user, GETDATE(), @detalii)";

                using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@actiune", System.Data.SqlDbType.NVarChar, 10).Value = actiune;
                    cmd.Parameters.Add("@user",    System.Data.SqlDbType.NVarChar, 50).Value = SessionInfo.Username;
                    cmd.Parameters.Add("@detalii", System.Data.SqlDbType.NVarChar, -1).Value = detalii ?? "";
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                AppLogger.Security($"[Drepturi] {actiune}: {detalii}");
            }
            catch (Exception ex)
            {
                // Auditul nu trebuie sa blocheze fluxul principal
                AppLogger.Error(ex, "PermissionService.AuditDrepturi");
            }
        }

        // ── Mesaje standard ─────────────────────────────────────────

        public static void AfiseazaMesajAcces()
        {
            System.Windows.Forms.MessageBox.Show(
                "Nu aveti drepturi de acces la aceasta functie.\n" +
                "Contacteaza administratorul pentru a-ti fi acordat accesul.",
                "Acces restrictionat",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }
    }
}
