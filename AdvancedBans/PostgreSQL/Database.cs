using NLog;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AdvancedBans
{
    /// <summary>
    /// The QueryManager is responsible for handling all database queries asynchronously.
    /// </summary>
    public class Database
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private string DBName;
        private string DBUser;
        private string DBAddress;
        private int DBPort;
        private string DBPassword;

        public NpgsqlConnection connection;

        private readonly string _fullConnString;

        public Database()
        {
            LoadConfig();
            // Build the full connection string with password:
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = DBAddress,
                Port = DBPort,
                Database = DBName,
                Username = DBUser,
                Password = DBPassword   // always include this
            };
            _fullConnString = builder.ConnectionString;

            // Open your primary connection (if you still need it elsewhere)
            connection = new NpgsqlConnection(_fullConnString);
            if (AdvancedBans.Instance.Config.Debug)
                Log.Error("Opening Connection!");
            connection.Open();
            
            if (AdvancedBans.Instance.Config.Debug)
                Log.Error("DB Credentials Loaded, Attempting Creation.");
            DatabaseInitializer initializer = new DatabaseInitializer(this);
            initializer.initialize(connection);
        }
        private void LoadConfig()
        {
            DBName = AdvancedBans.Instance.Config.DatabaseName;
            DBUser = AdvancedBans.Instance.Config.Username;
            DBAddress = AdvancedBans.Instance.Config.LocalAddress;
            DBPort = AdvancedBans.Instance.Config.Port;
            DBPassword = AdvancedBans.Instance.Config.Password;
        }

        /// <summary>
        /// Opens a connection to the PostgreSQL database. Returns true if successful.
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            if (IsConnected())
                return true;

            var connStringBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = DBAddress,
                Port = DBPort,
                Database = DBName,
                Username = DBUser
            };

            // If password is "null", omit it (connect without password); otherwise, set it
            if (string.Equals(DBPassword, "null", StringComparison.OrdinalIgnoreCase))
            {
                // Do not set the password
            }
            else
            {
                connStringBuilder.Password = DBPassword;
            }

            //connStringBuilder.SslMode = SslMode.Require; // uncomment if you need SSL

            connection = new NpgsqlConnection(connStringBuilder.ConnectionString);

            try
            {
                await connection.OpenAsync();
                Log.Info($"PostgreSQL connected: {DBAddress}:{DBPort}/{DBName}");
                return true;
            }
            catch (SocketException ex)
            {
                Log.Error("Database Network error!\nError: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not connect to PostgreSQL");
                return false;
            }
        }

        /// <summary>
        /// Checks if the connection is still open.
        /// </summary>
        public bool IsConnected()
            => connection?.State == ConnectionState.Open;

        /// <summary>
        /// Closes the PostgreSQL connection if open.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected())
                return;
            try
            {
                connection.Close();
                Log.Info("PostgreSQL connection closed.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing PostgreSQL connection");
            }
        }

        /// <summary>
        /// Execute any SQL (DDL, DML or query), with optional named parameters.
        /// Returns a DataTable for SELECT/VALUES/WITH; otherwise null.
        /// </summary>
        public DataTable ExecuteAny(string sql, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(_fullConnString))
            {
                Log.Error("No valid connection string available.");
                return null;
            }

            try
            {
                // 1) Open a fresh connection for each command
                using (var conn = new NpgsqlConnection(_fullConnString))
                {
                    conn.Open();

                    // 2) Prepare command
                    var trimmed = sql.TrimStart();
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        if (parameters != null)
                        {
                            foreach (var kv in parameters)
                                cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                        }

                        // 3) If it’s a SELECT/VALUES/WITH, load into DataTable
                        if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                            || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
                            || trimmed.StartsWith("VALUES", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                    return null;

                                var table = new DataTable();
                                table.Load(reader);
                                return table;
                            }
                        }
                        else
                        {
                            // 4) Otherwise (INSERT/UPDATE/DELETE/CREATE/etc), just run it
                            cmd.ExecuteNonQuery();
                            return null;
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                if (AdvancedBans.Instance.Config.Debug)
                    Log.Error($"Database error!\n{ex.Message}\n{ex.StackTrace}");
                return null;
            }
            catch (SocketException ex)
            {
                Log.Error($"Database Network error!\n{ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Error while executing SQL!\n{ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        public string ExecuteScalar(string sql, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(_fullConnString))
            {
                Log.Error("No valid connection string available.");
                return null;
            }

            try
            {
                // 1) Open a fresh connection for each command
                using (var conn = new NpgsqlConnection(_fullConnString))
                {
                    conn.Open();

                    // 2) Prepare command
                    var trimmed = sql.TrimStart();
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        if (parameters != null)
                        {
                            foreach (var kv in parameters)
                                cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                        }
                            
                        else
                        {
                            // 4) Otherwise (INSERT/UPDATE/DELETE/CREATE/etc), just run it
                            
                            return (string)cmd.ExecuteScalar();
                        }
                    }
                }
                return null;
            }
            catch (NpgsqlException ex)
            {
                if (AdvancedBans.Instance.Config.Debug)
                    Log.Error($"Database error!\n{ex.Message}\n{ex.StackTrace}");
                return null;
            }
            catch (SocketException ex)
            {
                Log.Error($"Database Network error!\n{ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Error while executing SQL!\n{ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }



        /// <summary>
        /// Returns the first-column integer result of the given query.
        /// </summary>
        public async Task<int> ExecuteCount(
            string sql,
            Dictionary<string, object> parameters = null
        )
        {
            if (!IsConnected())
                throw new InvalidOperationException("Not connected to database");

            var cmd = new NpgsqlCommand(sql, connection);
            if (parameters != null)
            {
                foreach (var kv in parameters)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
            }

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }
}
