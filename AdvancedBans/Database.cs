using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;
using Torch.Managers;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace AdvancedBans
{
    class Database
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static string DBName;
        public static string DBUser;
        public static string DBAddress;
        public static int DBPort;

        public Database(string name, string address, int port, string username)
        {
            DBName = name;
            DBAddress = address;
            DBPort = port;
            DBUser = username;
        }

        /// <summary>
        /// On init of server, create the structure for the AdvancedBans database. The name is dependant on the DBName from the Database constructor. If it already exists, it will simply not do anything. 
        /// </summary>
        public void CreateDatabase()
        {
            //Log.Error("Creating Database!");

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};";
            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();
                //Log.Error("Connection to MySQL database opened successfully.");

                string createDatabaseQuery = $"CREATE DATABASE IF NOT EXISTS {DBName};";
                MySqlCommand createDatabaseCommand = new MySqlCommand(createDatabaseQuery, connection);
                createDatabaseCommand.ExecuteNonQuery();
                Log.Info("Database initialized.");

                connection.Close();
                return;
                //Log.Warn("Connection to MySQL database closed.");

            }
            catch (MySqlException ex)
            {
                Log.Error("Error: " + ex.Message);
                return;
            }
        }

        /// <summary>
        /// On init of server, create the structure for the bans table. If it already exists, it will simply not do anything.
        /// </summary>
        public void CreateDatabaseStructure()
        {
            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};";
            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();
                //Log.Error("Connection to MySQL database opened successfully.");

                string createStructure = @"SET SQL_MODE = ""NO_AUTO_VALUE_ON_ZERO"";
START TRANSACTION;
SET time_zone = ""+00:00"";

CREATE TABLE IF NOT EXISTS `bans` (
    `BanNumber` int(255) NOT NULL,
    `SteamID` bigint(255) NOT NULL,
    `ExpireDate` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
    `Reason` varchar(5000) NOT NULL,
    `IsPermanent` tinyint(1) NOT NULL,
    `IsExpired` tinyint(1) NOT NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;


ALTER TABLE `bans`
    ADD PRIMARY KEY(`BanNumber`);

ALTER TABLE `bans`
    MODIFY `BanNumber` int(255) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT = 1;
COMMIT;";
                using (MySqlCommand createBanNumberTableCommand = new MySqlCommand(createStructure, connection))
                {
                    createBanNumberTableCommand.ExecuteNonQuery();
                }
                Log.Info("Database structure initialized.");
                connection.Close();
                return;
                //Log.Warn("Connection to MySQL database closed.");
            }
            catch (MySqlException ex)
            {
                Log.Error("Error: " + ex.Message);
                return;
            }
        }

        /// <summary>
        /// Checks if any user should be unbanned. If IsPermanent = 1, they will remain banned. If unbanned, their info remains in the database, BUT IsExpired will be 1 instead of 0.
        /// </summary>
        public void CheckAndUnbanExpiredBans()
        {
            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};convert zero datetime=True;";

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string selectQuery = "SELECT BanNumber, SteamID, ExpireDate, IsPermanent, IsExpired FROM bans;";
                MySqlCommand selectCommand = new MySqlCommand(selectQuery, connection);
                using (MySqlDataReader reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int banNumber = Convert.ToInt32(reader["BanNumber"]);
                        string TempSteamID = Convert.ToString(reader["SteamID"]);
                        string expireDateString = reader["ExpireDate"].ToString();
                        int isPermanent = Convert.ToInt32(reader["IsPermanent"]);
                        int isExpired = Convert.ToInt32(reader["IsExpired"]);
                        

                        // Parse the ExpireDate to a DateTime object.
                        if (DateTime.TryParse(expireDateString, out DateTime expireDate))
                        {
                            // Compare the current date with the ExpireDate.
                            if (DateTime.Now > expireDate && isPermanent != 1 && isExpired != 1)
                            {
                                ulong steamID = ulong.Parse(TempSteamID);
                                Log.Info($"User {steamID} with Ban Number {banNumber} ban has expired.");
                                SetExpired(banNumber);
                                MyMultiplayerBase.BanUser(steamID, false);
                            }
                        }
                    }
                    reader.Close();
                }
                connection.Close();
            }
        }
        private static void SetExpired(int banNumber)
        {
            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};";
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                // Update the IsExpired value to 1.
                string updateQuery = "UPDATE bans SET IsExpired = 1 WHERE BanNumber = @BanNumber;";
                MySqlCommand updateCommand = new MySqlCommand(updateQuery, connection);
                updateCommand.Parameters.AddWithValue("@BanNumber", banNumber);
                updateCommand.ExecuteNonQuery();
                connection.Close();
            }
            
        }

        /// <summary>
        /// Check if the user exists in the database. If so, it returns a JSON string.
        /// </summary>
        /// <param name="steamid"></param>
        /// <returns></returns>
        public string IsBanned(ulong steamid)
        {

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};";
            Dictionary<string, object> banInfo = new Dictionary<string, object>();
            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();

                // Your query to insert a new record into the "bans" table.
                string insertQuery = $"SELECT BanNumber, SteamID, Reason, ExpireDate, IsPermanent, IsExpired FROM bans WHERE SteamID = @steamid;";

                MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection);
                insertCommand.Parameters.AddWithValue("@steamid", steamid);

                using (MySqlDataReader reader = insertCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        banInfo["success"] = true;
                        banInfo["BanNumber"] = reader["BanNumber"];
                        banInfo["SteamID"] = reader["SteamID"];
                        banInfo["Reason"] = reader["Reason"];
                        banInfo["ExpireDate"] = reader["ExpireDate"];
                        banInfo["IsPermanent"] = reader["IsPermanent"];
                        banInfo["IsExpired"] = reader["IsExpired"];
                    } else {
                        banInfo["success"] = false;
                    }
                }

                connection.Close();
                return banInfo.Count > 0 ? JsonConvert.SerializeObject(banInfo, Formatting.Indented) : "{}";
            }
            catch (MySqlException ex)
            {
                Log.Error("Error: " + ex.Message);
                return "{}";
            }

            //Log.Warn("Connection to MySQL database closed.");
        }

    
        /// <summary>
        /// Add a user to the database.
        /// </summary>
        /// <param name="steamid"></param>
        /// <param name="expireDate"></param>
        /// <param name="reason"></param>
        /// <param name="isPermanent"></param>
        public void AddUser(ulong steamid, string expireDate, string reason, bool isPermanent)
        {

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};";
            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();
                //Log.Error("Connection to MySQL database opened successfully.");
                int permanentVal;
                if (isPermanent == true)
                {
                    permanentVal = 1;
                    expireDate = ""; //Null expire date
                }
                else
                {
                    permanentVal = 0;
                    // Parse the provided time string (e.g., "10m" for 10 minutes)
                    if (expireDate.EndsWith("m") && int.TryParse(expireDate.Substring(0, expireDate.Length - 1), out int minutes))
                    {
                        // Calculate the expiration time in minutes
                        DateTime expirationTime = DateTime.Now.AddMinutes(minutes);
                        expireDate = expirationTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else if (expireDate.EndsWith("h") && int.TryParse(expireDate.Substring(0, expireDate.Length - 1), out int hours))
                    {
                        // Calculate the expiration time in hours
                        DateTime expirationTime = DateTime.Now.AddHours(hours);
                        expireDate = expirationTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else if (expireDate.EndsWith("d") && int.TryParse(expireDate.Substring(0, expireDate.Length - 1), out int days))
                    {
                        // Calculate the expiration time in days
                        DateTime expirationTime = DateTime.Now.AddDays(days);
                        expireDate = expirationTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }

                string insertQuery = $"INSERT INTO `bans` (`SteamID`, `ExpireDate`, `Reason`, `IsPermanent`, `IsExpired`) VALUES ('{steamid}', '{expireDate}', '{reason}', '{permanentVal}', '0');";

                MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection);

                int rowsAffected = insertCommand.ExecuteNonQuery();

                MyMultiplayerBase.BanUser(steamid, true);

                Log.Info("Database structure initialized.");
                connection.Close();
                return;
            }
            catch (MySqlException ex)
            {
                Log.Error("Error: " + ex.Message);
                return;
            }
        }

        /// <summary>
        /// Removes a user from the Database. This is not recommended as it will remove all logs of the ban. To preserve the ban, use Unban(banNumber);
        /// </summary>
        /// <param name="banNumber"></param>
        public void RemoveUser(int banNumber)
        {

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};";
            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();
                //Log.Error("Connection to MySQL database opened successfully.");

                string insertQuery = $"DELETE FROM `bans` WHERE `bans`.`BanNumber` = {banNumber}";

                MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection);

                int rowsAffected = insertCommand.ExecuteNonQuery();

                Log.Info($"Removed banID: {banNumber} from Database.");
                connection.Close();
                return;
            }
            catch (MySqlException ex)
            {
                Log.Error("Error: " + ex.Message);
                return;
            }
        }

        /// <summary>
        /// Gets the entire database table, and return it to a json string.
        /// </summary>
        /// <returns></returns>
        public string AllListed()
        {

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};";

            List<Dictionary<string, object>> bansData = new List<Dictionary<string, object>>();

            MySqlConnection connection = new MySqlConnection(connectionString);

            connection.Open();
            //Log.Error("Connection to MySQL database opened successfully.");

            // Your query to insert a new record into the "bans" table.
            string selectQuery = "SELECT * FROM bans;";
            MySqlCommand selectCommand = new MySqlCommand(selectQuery, connection);

            using (MySqlDataReader reader = selectCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    Dictionary<string, object> banData = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        banData[reader.GetName(i)] = reader[i];
                    }
                    bansData.Add(banData);
                }
            }

            string json = JsonConvert.SerializeObject(bansData, Formatting.Indented);
            connection.Close();
            return json;
            //Log.Warn("Connection to MySQL database closed.");
        }
    }
}