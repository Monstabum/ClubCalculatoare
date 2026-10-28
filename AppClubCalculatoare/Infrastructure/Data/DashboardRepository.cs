using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace AppClubCalculatoare
{
    internal sealed class DashboardData
    {
        public int EchipamenteLibere { get; set; }
        public int TotalEchipamente { get; set; }
        public int SesiuniInAsteptare { get; set; }
        public int TotalSesiuni { get; set; }
        public int RezervariProgramate { get; set; }
        public decimal IncasareTotala { get; set; }
        public decimal Portofoliu { get; set; }

        public int? AnSelectat { get; set; }
        public int? LunaSelectat { get; set; }
        public DateTime? UltimaDataSesiune { get; set; }

        public decimal TotalPerioada { get; set; }
        public int NumarPerioada { get; set; }
        public decimal TotalAnterior { get; set; }

        public List<DashboardLuna> Luni { get; } = new List<DashboardLuna>();
        public List<DashboardOperator> Operatori { get; } = new List<DashboardOperator>();
        public List<DashboardTip> Tipuri { get; } = new List<DashboardTip>();
        public List<DashboardAlerta> Alerte { get; } = new List<DashboardAlerta>();
        public List<int> Ani { get; } = new List<int>();
    }

    internal sealed class DashboardLuna
    {
        public DateTime Luna { get; set; }
        public decimal Total { get; set; }
        public int Numar { get; set; }
    }

    internal sealed class DashboardOperator
    {
        public string Nume { get; set; }
        public decimal Total { get; set; }
        public int Numar { get; set; }
    }

    internal sealed class DashboardTip
    {
        public string Tip { get; set; }
        public int Numar { get; set; }
    }

    internal sealed class DashboardAlerta
    {
        public string Categorie { get; set; }
        public int Numar { get; set; }
        public int? ZileMax { get; set; }
        public DateTime? DataMin { get; set; }
    }

    /// <summary>
    /// Citește tot tabloul de bord printr-un singur apel al procedurii
    /// usp_Dashboard_Get (7 result set-uri, un singur round trip).
    /// </summary>
    internal static class DashboardRepository
    {
        public static Task<DashboardData> IncarcaAsync(
            int? an,
            int? luna,
            bool autoPerioada,
            int zileStagnare,
            int zileSesiuni,
            int zileRezervari,
            CancellationToken ct = default)
        {
            return RetryPolicy.ExecuteAsync(async () =>
            {
                return await SqlDb.WithConnectionAsync(async (conn, token) =>
                {
                    await using (var cmd = new SqlCommand("dbo.usp_Dashboard_Get", conn)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = SqlDb.DefaultTimeoutSeconds
                    })
                    {
                        cmd.Parameters.Add("@an", SqlDbType.Int).Value =
                            an.HasValue ? (object)an.Value : DBNull.Value;
                        cmd.Parameters.Add("@luna", SqlDbType.Int).Value =
                            luna.HasValue ? (object)luna.Value : DBNull.Value;
                        cmd.Parameters.Add("@autoPerioada", SqlDbType.Bit).Value = autoPerioada;
                        cmd.Parameters.Add("@zileStagnare", SqlDbType.Int).Value = zileStagnare;
                        cmd.Parameters.Add("@zileSesiuni", SqlDbType.Int).Value = zileSesiuni;
                        cmd.Parameters.Add("@zileRezervari", SqlDbType.Int).Value = zileRezervari;

                        await using (SqlDataReader reader =
                            await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            var data = new DashboardData();

                            // 1. KPI + perioada rezolvata
                            if (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                data.EchipamenteLibere   = Convert.ToInt32(reader["EchipamenteLibere"]);
                                data.TotalEchipamente    = Convert.ToInt32(reader["TotalEchipamente"]);
                                data.SesiuniInAsteptare  = Convert.ToInt32(reader["SesiuniInAsteptare"]);
                                data.TotalSesiuni        = Convert.ToInt32(reader["TotalSesiuni"]);
                                data.RezervariProgramate = Convert.ToInt32(reader["RezervariProgramate"]);
                                data.IncasareTotala      = Convert.ToDecimal(reader["IncasareTotala"]);
                                data.Portofoliu          = Convert.ToDecimal(reader["Portofoliu"]);

                                data.AnSelectat = reader["AnSelectat"] == DBNull.Value
                                    ? (int?)null : Convert.ToInt32(reader["AnSelectat"]);
                                data.LunaSelectat = reader["LunaSelectat"] == DBNull.Value
                                    ? (int?)null : Convert.ToInt32(reader["LunaSelectat"]);
                                data.UltimaDataSesiune = reader["UltimaDataSesiune"] == DBNull.Value
                                    ? (DateTime?)null : Convert.ToDateTime(reader["UltimaDataSesiune"]);
                            }

                            // 2. Perioada + anterioara
                            if (await reader.NextResultAsync(token).ConfigureAwait(false)
                                && await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                data.TotalPerioada = Convert.ToDecimal(reader["TotalPerioada"]);
                                data.NumarPerioada = Convert.ToInt32(reader["NumarPerioada"]);
                                data.TotalAnterior = Convert.ToDecimal(reader["TotalAnterior"]);
                            }

                            // 3. Ultimele 6 luni
                            if (await reader.NextResultAsync(token).ConfigureAwait(false))
                            {
                                while (await reader.ReadAsync(token).ConfigureAwait(false))
                                {
                                    data.Luni.Add(new DashboardLuna
                                    {
                                        Luna   = reader.GetDateTime(0),
                                        Total  = Convert.ToDecimal(reader.GetValue(1)),
                                        Numar  = Convert.ToInt32(reader.GetValue(2))
                                    });
                                }
                            }

                            // 4. Top 5 operatori
                            if (await reader.NextResultAsync(token).ConfigureAwait(false))
                            {
                                while (await reader.ReadAsync(token).ConfigureAwait(false))
                                {
                                    data.Operatori.Add(new DashboardOperator
                                    {
                                        Nume   = reader.GetString(1),
                                        Total  = Convert.ToDecimal(reader.GetValue(2)),
                                        Numar  = Convert.ToInt32(reader.GetValue(3))
                                    });
                                }
                            }

                            // 5. Tipuri de echipament
                            if (await reader.NextResultAsync(token).ConfigureAwait(false))
                            {
                                while (await reader.ReadAsync(token).ConfigureAwait(false))
                                {
                                    data.Tipuri.Add(new DashboardTip
                                    {
                                        Tip   = reader.GetString(0),
                                        Numar = reader.GetInt32(1)
                                    });
                                }
                            }

                            // 6. Alerte
                            if (await reader.NextResultAsync(token).ConfigureAwait(false))
                            {
                                while (await reader.ReadAsync(token).ConfigureAwait(false))
                                {
                                    data.Alerte.Add(new DashboardAlerta
                                    {
                                        Categorie = reader.GetString(0),
                                        Numar     = reader.GetInt32(1),
                                        ZileMax   = reader.IsDBNull(2) ? (int?)null
                                                                    : reader.GetInt32(2),
                                        DataMin   = reader.IsDBNull(3) ? (DateTime?)null
                                                                    : reader.GetDateTime(3)
                                    });
                                }
                            }

                            // 7. Ani
                            if (await reader.NextResultAsync(token).ConfigureAwait(false))
                            {
                                while (await reader.ReadAsync(token).ConfigureAwait(false))
                                    data.Ani.Add(reader.GetInt32(0));
                            }

                            return data;
                        }
                    }
                }, ct).ConfigureAwait(false);
            }, ct);
        }
    }
}
