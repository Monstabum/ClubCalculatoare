using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Serviciul de echipamente favorite ale utilizatorului curent
    /// (tabela Favorite: id_utilizator + id_echipament).
    ///
    /// Permisiune: PuncteAcces.Favorite (verificata in UI + la actiune,
    /// ca orice alt punct de acces — vezi PermissionService).
    /// Toate interogarile sunt parametrizate (anti SQL injection).
    /// </summary>
    internal static class FavoriteService
    {
        private static int IdUtilizatorCurent()
        {
            const string sql = "SELECT id_utilizator FROM Utilizatori WHERE username = @user";
            using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@user", System.Data.SqlDbType.NVarChar, 50)
                              .Value = SessionInfo.Username;
                conn.Open();
                object rez = cmd.ExecuteScalar();
                int id;
                if (rez != null && int.TryParse(rez.ToString(), out id)) return id;
                throw new InvalidOperationException(
                    "Utilizatorul '" + SessionInfo.Username + "' nu a fost gasit in baza de date.");
            }
        }

        /// <summary>ID-urile echipamentelor favorite ale utilizatorului curent.</summary>
        public static HashSet<int> GetIduriFavorite()
        {
            var rez = new HashSet<int>();
            const string sql = @"
                SELECT id_echipament
                FROM   Favorite
                WHERE  id_utilizator = @id";
            using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = IdUtilizatorCurent();
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        rez.Add(reader.GetInt32(0));
            }
            return rez;
        }

        public static bool EsteFavorite(int idEchipament)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM   Favorite
                WHERE  id_utilizator = @idu AND id_echipament = @idp";
            using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@idu", System.Data.SqlDbType.Int).Value = IdUtilizatorCurent();
                cmd.Parameters.Add("@idp", System.Data.SqlDbType.Int).Value = idEchipament;
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>Adauga o echipament la favorite. Returneaza true daca s-a adaugat.</summary>
        public static bool Adauga(int idEchipament)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM Favorite
                               WHERE id_utilizator = @idu AND id_echipament = @idp)
                    INSERT INTO Favorite (id_utilizator, id_echipament)
                    VALUES (@idu, @idp)";
            using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@idu", System.Data.SqlDbType.Int).Value = IdUtilizatorCurent();
                cmd.Parameters.Add("@idp", System.Data.SqlDbType.Int).Value = idEchipament;
                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>Sterge o echipament din favorite. Returneaza true daca s-a sters.</summary>
        public static bool Sterge(int idEchipament)
        {
            const string sql = @"
                DELETE FROM Favorite
                WHERE  id_utilizator = @idu AND id_echipament = @idp";
            using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@idu", System.Data.SqlDbType.Int).Value = IdUtilizatorCurent();
                cmd.Parameters.Add("@idp", System.Data.SqlDbType.Int).Value = idEchipament;
                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>
        /// Comută starea de favorite pentru o echipament.
        /// Returneaza noul statut (true = este acum favorite).
        /// FIX: toggle atomic intr-o singura comanda SQL (fara cursa
        /// check-then-act: doua apeluri paralele nu mai pot dubla INSERT-ul).
        /// </summary>
        public static bool Comuteaza(int idEchipament)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM Favorite
                            WHERE id_utilizator = @idu AND id_echipament = @idp)
                BEGIN
                    DELETE FROM Favorite
                    WHERE id_utilizator = @idu AND id_echipament = @idp;
                    SELECT 0;
                END
                ELSE
                BEGIN
                    INSERT INTO Favorite (id_utilizator, id_echipament)
                    VALUES (@idu, @idp);
                    SELECT 1;
                END";
            try
            {
                using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@idu", System.Data.SqlDbType.Int).Value = IdUtilizatorCurent();
                    cmd.Parameters.Add("@idp", System.Data.SqlDbType.Int).Value = idEchipament;
                    conn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FavoriteService.Comuteaza");
                // Fallback pe vechea cale (non-atomica) la eroare neasteptata.
                bool era = EsteFavorite(idEchipament);
                if (era) Sterge(idEchipament);
                else     Adauga(idEchipament);
                return !era;
            }
        }

        /// <summary>
        /// Tabela Favorite exista in baza? (scriptul BD/Favorite.sql rulat?)
        /// Folosit pentru mesaje clare in loc de SqlException.
        /// </summary>
        public static bool TabelaExista()
        {
            try
            {
                const string sql = "SELECT COUNT(*) FROM Favorite";
                using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.ExecuteScalar();
                    return true;
                }
            }
            catch (SqlException) { return false; }
        }
    }
}
