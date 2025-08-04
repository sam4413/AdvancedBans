using NLog;
using NLog.Fluent;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AdvancedBans
{
    /// <summary>  
    /// Initializes the database structure.  
    /// </summary>  
    internal class DatabaseInitializer
    {
        private Database db;

        public DatabaseInitializer(Database database)
        {
            db = database;
            if (AdvancedBans.Instance.Config.Debug)
                Log.Error("Called DB Init!");
        }

        private static readonly StringBuilder TABLES = new StringBuilder()
            .Append(@"CREATE TABLE IF NOT EXISTS ""bans"" (
            ""SteamID"" bigint NOT NULL,
			""CaseID"" varchar(5000) NOT NULL,
            ""BannedDate"" timestamp NULL DEFAULT CURRENT_TIMESTAMP,
			""ExpireDate"" timestamp NULL DEFAULT CURRENT_TIMESTAMP,
			""Reason"" varchar(5000) NOT NULL,
            ""IsPermanent"" smallint NOT NULL,
            ""IsExpired"" smallint NOT NULL,
            ""EnforceIP"" smallint NOT NULL
        );")
            .Append(@"CREATE TABLE IF NOT EXISTS ""ip"" (
            ""SteamID"" bigint NOT NULL,
			""Address"" varchar(5000) NOT NULL,
            ""LastKnownDate"" timestamp NULL DEFAULT CURRENT_TIMESTAMP
        );").TrimTrailingWhitespace();

        public void initialize(NpgsqlConnection connection)
        {
            if (AdvancedBans.Instance.Config.Debug)
                Log.Error("Calling DB Creation!");
            CreateDatabase();
            if (AdvancedBans.Instance.Config.Debug)
                Log.Error("Calling DB Table creation!");
            // make sure your plugin has opened db.connection...
            if (!db.IsConnected())
                db.ConnectAsync().GetAwaiter().GetResult();

            // run both statements in one go:
            var cmd = new NpgsqlCommand(TABLES.ToString(), db.connection);
            cmd.ExecuteNonQuery();
            if (AdvancedBans.Instance.Config.Debug)
                Log.Error("DB Init Complete!");
        }




        /// <summary>
        /// On init of server, create the structure for the AdvancedBans database. The name is dependant on the DBName from the Database constructor. If it already exists, it will simply not do anything. 
        /// </summary>
        public void CreateDatabase()
        {
            var cfg = AdvancedBans.Instance.Config;
            // 1. Connect to the maintenance DB ("postgres") on your real server:
            var maintenanceCs = new NpgsqlConnectionStringBuilder
            {
                Host = cfg.LocalAddress,     // ← use LocalAddress, not DatabaseName
                Port = cfg.Port,
                Username = cfg.Username,
                Password = cfg.Password,
                Database = "postgres"
            }.ConnectionString;

            using (var conn = new NpgsqlConnection(maintenanceCs))
            {
                conn.Open();
                // check for existence…
                var check = new NpgsqlCommand(
                    $"SELECT 1 FROM pg_database WHERE datname='{cfg.DatabaseName}'",
                    conn
                ).ExecuteScalar();

                if (check == null)
                {
                    // create it
                    var create = new NpgsqlCommand(
                        $"CREATE DATABASE \"{cfg.DatabaseName}\"",
                        conn
                    );
                    create.ExecuteNonQuery();
                    Log.Info("Database created.");
                }
                else
                {
                    Log.Info("Database already exists.");
                }
            }

            // 2. Now that it exists, open your actual game-DB connection:
            var appCs = new NpgsqlConnectionStringBuilder
            {
                Host = cfg.LocalAddress,
                Port = cfg.Port,
                Username = cfg.Username,
                Password = cfg.Password,
                Database = cfg.DatabaseName
            }.ConnectionString;

            db.connection = new NpgsqlConnection(appCs);
            db.connection.Open();
        }
    }
}
