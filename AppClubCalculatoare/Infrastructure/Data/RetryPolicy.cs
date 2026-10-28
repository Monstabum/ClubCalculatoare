using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Retry doar pentru erori tranzitorii de rețea / disponibilitate SQL.
    /// Nu se folosește pentru scrieri: un INSERT repetat ar putea duplica date.
    /// </summary>
    internal static class RetryPolicy
    {
        private const int MaxAttempts = 3;
        private const int BaseDelayMs = 200;

        /// <summary>Erori pentru care o reîncercare are sens (conexiune, failover, deadlock).</summary>
        public static bool EsteTranzitorie(Exception ex)
        {
            if (!(ex is SqlException sql)) return false;

            switch (sql.Number)
            {
                case -2:      // timeout de comandă (poate fi o blocare temporară)
                case 20:
                case 64:
                case 233:
                case 1205:    // deadlock victim
                case 4060:
                case 40197:
                case 40501:
                case 40613:
                case 49918:
                case 49919:
                case 49920:
                case 10053:
                case 10054:
                case 10060:
                case 11001:
                    return true;
                default:
                    return false;
            }
        }

        public static async Task<T> ExecuteAsync<T>(
            Func<Task<T>> actiune,
            CancellationToken ct = default)
        {
            if (actiune == null) throw new ArgumentNullException(nameof(actiune));

            for (int incercare = 1; ; incercare++)
            {
                try
                {
                    return await actiune().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (
                    incercare < MaxAttempts &&
                    !ct.IsCancellationRequested &&
                    EsteTranzitorie(ex))
                {
                    int delay = BaseDelayMs * (1 << (incercare - 1));
                    AppLogger.Warn(
                        $"SQL tranzitoriu ({ex.Message}); reîncerc {incercare + 1}/{MaxAttempts} " +
                        $"după {delay} ms.", "RetryPolicy");
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }
    }
}
