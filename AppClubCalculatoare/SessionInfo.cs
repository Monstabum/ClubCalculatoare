using System.Collections.Generic;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Retine informatiile sesiunii curente (utilizatorul autentificat).
    /// Setat in FormLogin la autentificare reusita.
    /// Folosit in AuditLog pentru a inregistra cine a facut modificarile.
    ///
    /// Permisiuni: setul de coduri de puncte de acces acordate individual
    /// utilizatorului (tabela UtilizatoriPermisiuni), incarcat de
    /// PermissionService la autentificare. Administratorul are automat
    /// acces la toate punctele, indiferent de continutul setului.
    /// </summary>
    internal static class SessionInfo
    {
        public static string Username     { get; set; } = "necunoscut";
        public static string NumeComplet  { get; set; } = "";
        public static string Rol          { get; set; } = "";

        // ── Permisiunile punctelor de acces (Q — sistem drepturi) ───
        public static HashSet<string> Permisiuni { get; }
            = new HashSet<string>();

        /// <summary>Adminul are acces la toate punctele; ceilalti utilizatori
        /// doar la codurile acordate explicit in UtilizatoriPermisiuni.</summary>
        public static bool EsteAdmin => Rol == "Administrator";

        public static bool ArePermisiune(string cod)
            => EsteAdmin || Permisiuni.Contains(cod);

        public static void Reset()
        {
            Username    = "necunoscut";
            NumeComplet = "";
            Rol         = "";
            Permisiuni.Clear();
        }
    }
}
