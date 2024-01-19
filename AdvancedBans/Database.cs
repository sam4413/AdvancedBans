using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySqlConnector;
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
using AdvancedBans.AdvancedBans;
using Sandbox.Game.World;

namespace AdvancedBans
{
    class Database
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static string DBName;
        public static string DBUser;
        public static string DBAddress;
        public static int DBPort;
        internal static string DBPassword;

        public static int LatestBanNumber;
        public static string LatestBanReason;
        public static long LatestBanSteamID;
        public static DateTime LatestBanExpireDate;
        public static byte LatestIsPermanentBan;
        public static byte LatestIsExpired;

        public static bool DebugMode;
        public Database(string name, string address, int port, string username, string password)
        {
            DBName = name;
            DBAddress = address;
            DBPort = port;
            DBUser = username;
            DBPassword = password;
        }

        /// <summary>
        /// On init of server, create the structure for the AdvancedBans database. The name is dependant on the DBName from the Database constructor. If it already exists, it will simply not do anything. 
        /// </summary>
        public void CreateDatabase()
        {
            string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};" + passwordSegment;

            MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();

            string createDatabaseQuery = $"CREATE DATABASE IF NOT EXISTS {DBName};";
            MySqlCommand createDatabaseCommand = new MySqlCommand(createDatabaseQuery, connection);
            createDatabaseCommand.ExecuteNonQuery();
            Log.Info("Database initialized.");

            connection.Close();
            return;

        }

        /// <summary>
        /// On init of server, create the structure for the bans table. If it already exists, it will simply not do anything.
        /// </summary>
        public void CreateDatabaseStructure()
        {
            string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;
            MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();

            string createStructure = @"SET SQL_MODE = ""NO_AUTO_VALUE_ON_ZERO"";
            START TRANSACTION;
            SET time_zone = ""+00:00"";

            CREATE TABLE IF NOT EXISTS `bans` (
            `BanNumber` int(255) NOT NULL AUTO_INCREMENT,
            `SteamID` bigint(255) NOT NULL,
            `ExpireDate` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
            `Reason` varchar(5000) NOT NULL,
            `IsPermanent` tinyint(1) NOT NULL,
            `IsExpired` tinyint(1) NOT NULL,
            PRIMARY KEY(`BanNumber`)
            ) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;
            COMMIT;";
            using (MySqlCommand createBanNumberTableCommand = new MySqlCommand(createStructure, connection))
            {
                createBanNumberTableCommand.ExecuteNonQuery();
            }
            Log.Info("Database structure initialized.");
            connection.Close();
            return;
        }

        /// <summary>
        /// Checks if any user should be unbanned. If IsPermanent = 1, they will remain banned. If unbanned, their info remains in the database, BUT IsExpired will be 1 instead of 0.
        /// </summary>
        public void CheckAndUnbanExpiredBans()
        {

            string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};convert zero datetime=True;" + passwordSegment;

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

                        if (DateTime.TryParse(expireDateString, out DateTime expireDate))
                        {
                            if (DateTime.Now > expireDate && isPermanent != 1 && isExpired != 1)
                            {
                                ulong steamID = ulong.Parse(TempSteamID);
                                Log.Info($"User {steamID} with Ban Number {banNumber} ban has expired.");
                                SetExpired(banNumber);
                            }
                        }
                    }
                    reader.Close();
                }
                connection.Close();
            }
        }
        public static void SetExpired(int banNumber)
        {
            string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";
            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;
            ulong steamID = 0;
            bool debug = AdvancedBansPatches.Debug;
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string selectQuery = "SELECT SteamID FROM bans WHERE BanNumber = @BanNumber;";
                MySqlCommand selectCommand = new MySqlCommand(selectQuery, connection);
                selectCommand.Parameters.AddWithValue("@BanNumber", banNumber);

                using (MySqlDataReader reader = selectCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        steamID = reader.GetUInt64("SteamID");
                    }
                }

                if (steamID != 0)
                {
                    Log.Info($"Setting ban {banNumber} ({steamID}) to expired.");

                    string updateQuery = "UPDATE bans SET IsExpired = 1 WHERE BanNumber = @BanNumber;";
                    MySqlCommand updateCommand = new MySqlCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@BanNumber", banNumber);
                    updateCommand.ExecuteNonQuery();

                    MyMultiplayerBase.BanUser(steamID, false);
                }
                else
                {
                    if (debug == true) Log.Error("No SteamID found for ban " + banNumber);
                }
                connection.Close();
            }
        }


        /// <summary>
        /// Check if the user exists in the database, and if isExpired is 1. If they are still banned, it will return true. Else, it will return false.
        /// </summary>
        /// <param name="steamid"></param>
        /// <returns></returns>
        public bool IsBanned(ulong steamid)
        {
            string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};Convert Zero Datetime=True;" + passwordSegment;

            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();


                string insertQuery = "SELECT BanNumber, SteamID, Reason, ExpireDate, IsPermanent, IsExpired " +
                             "FROM bans WHERE SteamID = @steamid " +
                             "ORDER BY BanNumber DESC LIMIT 1;";

                MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection);
                insertCommand.Parameters.AddWithValue("@steamid", steamid);

                var MyResult = false;
                using (MySqlDataReader reader = insertCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        LatestBanNumber = reader.GetInt32("BanNumber");
                        LatestBanSteamID = reader.GetInt64("SteamID");
                        LatestBanReason = reader.GetString("Reason");
                        LatestBanExpireDate = reader.GetDateTime("ExpireDate");
                        LatestIsPermanentBan = reader.GetByte("IsPermanent");
                        LatestIsExpired = reader.GetByte("IsExpired");

                        if (LatestIsExpired == 1)
                        {
                            MyResult = false;
                        }
                        else if (LatestIsExpired == 0)
                        {
                            MyResult = true;
                        }
                        else
                        {
                            MyResult = true;
                        }
                    }
                }

                connection.Close();
                return MyResult;
            }
            catch (MySqlException ex)
            {
                Log.Error("Error: " + ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Add a user to the database.
        /// </summary>
        /// <param name="steamid"></param>
        /// <param name="expireDate"></param>
        /// <param name="reason"></param>
        /// <param name="isPermanent"></param>
        public int AddUser(ulong steamid, string expireDate, string reason, bool isPermanent)
        {
            string WMPublicAddress = AdvancedBansPatches.WMPublicAddress;

            string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;

            MySqlConnection connection = new MySqlConnection(connectionString);
            bool debug = AdvancedBansPatches.Debug;
            try
            {
                connection.Open();
                int permanentVal;
                if (isPermanent == true)
                {
                    permanentVal = 1;
                    expireDate = "";
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

                long MyBanNumber = insertCommand.LastInsertedId;

                long playerId = MySession.Static.Players.TryGetIdentityId(steamid);
                if (debug == true)
                {
                    Log.Error(playerId);
                    Log.Error("Displaying message");
                }

                IsBanned(steamid);

                string MyUrl = $"http://{WMPublicAddress}:{WebServer.WMPort}/?BanNumber={LatestBanNumber}&SteamID={LatestBanSteamID}&ExpireDate={LatestBanExpireDate.ToString("g")}&IsPermanent={LatestIsPermanentBan}&Reason={LatestBanReason}";
                Log.Error(MyUrl);
                MyVisualScriptLogicProvider.OpenSteamOverlay($"https://steamcommunity.com/linkfilter/?url={MyUrl}", playerId);

                MyMultiplayerBase.BanUser(steamid, true);
                if (debug == true)
                {
                    Log.Error($@"
    ===Ban Info===
    Ban Number: {LatestBanNumber}
    SteamId: {LatestBanSteamID}
    Expire Date: {LatestBanExpireDate.ToString("g")}
    Is Permanent: {LatestIsPermanentBan}
    Is Expired: {LatestIsExpired}
    ");
                }

                Log.Info($"User {steamid} was banned with Ban Number {LatestBanNumber}");
                if (AdvancedBansPlugin.Instance.Config.AM_BanButton == false)
                {
                    MyMultiplayerBase.BanUser(steamid, false);
                }

                if (debug == true) Log.Info("Database structure initialized.");

                connection.Close();
                return (int)MyBanNumber;
            }
            catch (MySqlException ex)
            {
                Log.Error("Error: " + ex.Message);
                return -1;
            }
        }

        /// <summary>
        /// Removes a user from the Database. This is not recommended as it will remove all logs of the ban. To preserve the ban, use SetExpired(banNumber);
        /// </summary>
        /// <param name="banNumber"></param>
        public void RemoveUser(int banNumber)
        {

            string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;

            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();

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
        public static string AllListed()
        {

            string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

            string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};convert zero datetime=True;" + passwordSegment;

            List<Dictionary<string, object>> bansData = new List<Dictionary<string, object>>();

            MySqlConnection connection = new MySqlConnection(connectionString);

            connection.Open();

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
        }
    }
}