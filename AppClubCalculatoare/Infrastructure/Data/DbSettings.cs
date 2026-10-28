using System;
using Microsoft.Data.SqlClient;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Normalizează connection string-ul din App.config fără a suprascrie
    /// opțiunile explicite ale utilizatorului (Encrypt, autentificare etc.).
    /// </summary>
    internal static class DbSettings
    {
        private const string NumeAplicatie = "AppClubCalculatoare";

        public static string Build(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string lipsă.", nameof(connectionString));

            var builder = new SqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(builder.ApplicationName))
                builder.ApplicationName = NumeAplicatie;

            // Planul cere un fail-fast de 5s la conectare; valoarea 15 este
            // default-ul ADO.NET, deci o tratăm ca „nespecificat".
            if (builder.ConnectTimeout == 15)
                builder.ConnectTimeout = 5;

            // Reîncercare la nivel de conectare (MDS 5+/7): utilă la Wi-Fi/VPN.
            if (builder.ConnectRetryCount == 1)
            {
                builder.ConnectRetryCount = 3;
                builder.ConnectRetryInterval = 5;
            }

            return builder.ConnectionString;
        }
    }
}
