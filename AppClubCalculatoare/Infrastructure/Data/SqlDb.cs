using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Punct unic de acces ADO.NET: creează conexiuni din connection string-ul
    /// configurat, aplică timeout-uri și oferă helperi async pentru query-uri,
    /// scalar, DataTable și tranzacții. Formele nu mai creează SqlConnection direct.
    /// </summary>
    internal static class SqlDb
    {
        public const int DefaultTimeoutSeconds = 30;
        public const int ReportTimeoutSeconds = 120;

        public static string ConnectionString => DatabaseConfig.ConnectionString;

        /// <summary>Conexiune nouă, închisă. Se folosește în helperii de mai jos.</summary>
        public static SqlConnection CreateConnection()
            => new SqlConnection(ConnectionString);

        /// <summary>
        /// Deschide o conexiune, rulează corpul și o închide garantat.
        /// Orice eroare este propagată apelantului.
        /// </summary>
        public static async Task<T> WithConnectionAsync<T>(
            Func<SqlConnection, CancellationToken, Task<T>> body,
            CancellationToken ct = default)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            await using (SqlConnection conn = CreateConnection())
            {
                await conn.OpenAsync(ct).ConfigureAwait(false);
                return await body(conn, ct).ConfigureAwait(false);
            }
        }

        /// <summary>INSERT/UPDATE/DELETE; fără retry (scrierile nu sunt idempotente).</summary>
        public static Task<int> ExecuteAsync(
            string sql,
            Action<SqlParameterCollection> bind = null,
            CancellationToken ct = default,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            return WithConnectionAsync(async (conn, token) =>
            {
                await using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds })
                {
                    bind?.Invoke(cmd.Parameters);
                    return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }, ct);
        }

        /// <summary>Valoare scalară; întoarce default dacă rezultatul e NULL.</summary>
        public static Task<T> ScalarAsync<T>(
            string sql,
            Action<SqlParameterCollection> bind = null,
            CancellationToken ct = default,
            int timeoutSeconds = DefaultTimeoutSeconds,
            bool retryTransient = true)
        {
            Func<Task<T>> operatie = async () =>
            {
                return await WithConnectionAsync(async (conn, token) =>
                {
                    await using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds })
                    {
                        bind?.Invoke(cmd.Parameters);
                        object value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
                        if (value == null || value == DBNull.Value) return default(T);
                        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                    }
                }, ct).ConfigureAwait(false);
            };

            return retryTransient
                ? RetryPolicy.ExecuteAsync(operatie, ct)
                : operatie();
        }

        /// <summary>Listă materializată prin mapper, cu retry doar pe erori tranzitorii.</summary>
        public static Task<List<T>> QueryAsync<T>(
            string sql,
            Func<SqlDataReader, T> map,
            Action<SqlParameterCollection> bind = null,
            CancellationToken ct = default,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            return RetryPolicy.ExecuteAsync(async () =>
            {
                return await WithConnectionAsync(async (conn, token) =>
                {
                    await using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds })
                    {
                        bind?.Invoke(cmd.Parameters);
                        var rezultat = new List<T>();
                        await using (SqlDataReader reader =
                            await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                                rezultat.Add(map(reader));
                        }
                        return rezultat;
                    }
                }, ct).ConfigureAwait(false);
            }, ct);
        }

        /// <summary>
        /// DataTable pentru listele legate direct la DataGridView. Citirea din
        /// reader este locală; rețeaua se consumă asincron în ExecuteReaderAsync.
        /// </summary>
        public static Task<DataTable> QueryDataTableAsync(
            string sql,
            Action<SqlParameterCollection> bind = null,
            CancellationToken ct = default,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            return RetryPolicy.ExecuteAsync(async () =>
            {
                return await WithConnectionAsync(async (conn, token) =>
                {
                    await using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds })
                    {
                        bind?.Invoke(cmd.Parameters);
                        await using (SqlDataReader reader =
                            await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            var dt = new DataTable();
                            dt.Load(reader);
                            return dt;
                        }
                    }
                }, ct).ConfigureAwait(false);
            }, ct);
        }

        /// <summary>Execută un corp într-o tranzacție, cu rollback la excepție.</summary>
        public static async Task<T> InTransactionAsync<T>(
            Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> body,
            CancellationToken ct = default,
            IsolationLevel isolation = IsolationLevel.ReadCommitted)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            return await WithConnectionAsync(async (conn, token) =>
            {
                await using (SqlTransaction tx =
                    (SqlTransaction)await conn.BeginTransactionAsync(isolation, token)
                        .ConfigureAwait(false))
                {
                    try
                    {
                        T result = await body(conn, tx, token).ConfigureAwait(false);
                        await tx.CommitAsync(token).ConfigureAwait(false);
                        return result;
                    }
                    catch
                    {
                        try
                        {
                            await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Conexiunea poate fi deja întreruptă; excepția originală rămâne.
                        }
                        throw;
                    }
                }
            }, ct).ConfigureAwait(false);
        }

        public static Task InTransactionAsync(
            Func<SqlConnection, SqlTransaction, CancellationToken, Task> body,
            CancellationToken ct = default,
            IsolationLevel isolation = IsolationLevel.ReadCommitted)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return InTransactionAsync<object>(async (conn, tx, token) =>
            {
                await body(conn, tx, token).ConfigureAwait(false);
                return null;
            }, ct, isolation);
        }
    }
}
