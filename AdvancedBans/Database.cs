using System;
using System.Collections.Generic;
using Npgsql;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using AdvancedBans.AdvancedBans;
using Sandbox.Game.World;
using System.Text.Json;
using System.Data;
using System.Diagnostics;
using static Sandbox.Game.AI.Pathfinding.Obsolete.MyGridPathfinding;
using System.Text.Json.Nodes;
using System.Text;
using VRage.Game.Entity;
using VRage.ModAPI;
using Sandbox.Game.Entities.Character;
using System.Threading.Tasks;

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

		internal static bool DebugMode = AdvancedBansPatches.Debug;

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
			string connectionString = $"Host={DBAddress};Port={DBPort};Username={DBUser};Password={DBPassword};Database=postgres;";  // Connect to the default 'postgres' database
			NpgsqlConnection connection = new NpgsqlConnection(connectionString);

			string checkDbExistsQuery = $"SELECT 1 FROM pg_database WHERE datname='{DBName}'";
			NpgsqlCommand checkDbExistsCommand = new NpgsqlCommand(checkDbExistsQuery, connection);

			try
			{
				connection.Open();
				var exists = checkDbExistsCommand.ExecuteScalar() != null;

				if (!exists)
				{
					connection.Close(); // Close the existing connection

					// Now open a new connection to 'postgres' database to create the new database
					using (var createDbConnection = new NpgsqlConnection(connectionString))
					{
						createDbConnection.Open();
						string createDatabaseQuery = $"CREATE DATABASE \"{DBName}\"";
						NpgsqlCommand createDatabaseCommand = new NpgsqlCommand(createDatabaseQuery, createDbConnection);
						createDatabaseCommand.ExecuteNonQuery();
						Log.Info("Database created.");
					}
				}
				else
				{
					Log.Info("Database already exists.");
				}
			}
			catch (NpgsqlException ex)
			{
				Log.Error($"Error in CreateDatabase: {ex.Message}");
				throw;
			}
			finally
			{
				if (connection.State == ConnectionState.Open)
					connection.Close();
			}
		}



		/// <summary>
		/// On init of server, create the structure for the bans table. If it already exists, it will simply not do anything.
		/// </summary>
		public void CreateDatabaseStructure()
		{
			string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};Password={DBPassword};";
			NpgsqlConnection connection = new NpgsqlConnection(connectionString);

			try
			{
				connection.Open();
				string createStructure = @"
            CREATE TABLE IF NOT EXISTS ""bans"" (
            ""SteamID"" bigint NOT NULL,
			""CaseID"" varchar(5000) NOT NULL,
            ""BannedDate"" timestamp NULL DEFAULT CURRENT_TIMESTAMP,
			""ExpireDate"" timestamp NULL DEFAULT CURRENT_TIMESTAMP,
			""Reason"" varchar(5000) NOT NULL,
            ""IsPermanent"" smallint NOT NULL,
            ""IsExpired"" smallint NOT NULL
        );";
				using (NpgsqlCommand createBanNumberTableCommand = new NpgsqlCommand(createStructure, connection))
				{
					createBanNumberTableCommand.ExecuteNonQuery();
				}
				Log.Info("Database structure initialized.");
			}
			catch (NpgsqlException ex)
			{
				Log.Error($"Error in CreateDatabaseStructure: {ex.Message}");
				throw;
			}
			finally
			{
				connection.Close();
			}
		}


		/// <summary>
		/// Checks if any user should be unbanned. It will also check if a user should be banned. If IsPermanent = 1, they will remain banned. If unbanned, their info remains in the database, BUT IsExpired will be 1 instead of 0.
		/// </summary>
		public async void CheckAndUnbanExpiredBans()
		{
			string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";
			string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;

			using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
			{
				connection.Open();

				string selectQuery = @"SELECT ""SteamID"", ""CaseID"", ""BannedDate"", ""ExpireDate"", ""Reason"", ""IsPermanent"", ""IsExpired""
                               FROM ""bans"";";
				NpgsqlCommand selectCommand = new NpgsqlCommand(selectQuery, connection);
				using (NpgsqlDataReader reader = selectCommand.ExecuteReader())
				{
					while (reader.Read())
					{
						string SteamIDString = Convert.ToString(reader["SteamID"]);
						string CaseID = Convert.ToString(reader["CaseID"]);
						string ExpireDateString = reader["ExpireDate"] == DBNull.Value ? null : reader["ExpireDate"].ToString();
						int isPermanent = Convert.ToByte(reader["IsPermanent"]);
						int isExpired = Convert.ToByte(reader["IsExpired"]);

						if (DateTime.TryParse(ExpireDateString, out DateTime expireDate))
						{
							if (DateTime.Now > expireDate && isPermanent != 1 && isExpired != 1)
							{
								ulong steamID = ulong.Parse(SteamIDString);
								if (GetBannedByCaseID(CaseID) != null)
								{
									Log.Info($"User {steamID} (CaseID: {CaseID}) ban has expired.");
									SetExpired(CaseID);
								}
								else
								{
									if (DebugMode)
									{
										Log.Error($"Internal error. User {steamID} (CaseID: {CaseID}) could not be unbanned.");
									}
								}
							}
						}
					}
					reader.Close();
				}
				connection.Close();
			}
		}


		/// <summary>
		/// Sets a ban's status to expired based on caseId, unbanning the user. Success = 0, Already Expired = 1, Not found = 2
		/// </summary>
		/// <param name="caseid"></param>
		/// <returns>An integer 0-2</returns>
		public static int SetExpired(string caseid)
		{
			string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";
			string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;

			ulong steamID = 0;
			int isAlreadyExpired;

			using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
			{
				connection.Open();

				string selectQuery = @"SELECT ""IsExpired"", ""SteamID"" FROM ""bans"" WHERE ""CaseID"" = @caseID;";
				NpgsqlCommand selectCommand = new NpgsqlCommand(selectQuery, connection);
				selectCommand.Parameters.AddWithValue("@caseID", caseid);

				using (NpgsqlDataReader reader = selectCommand.ExecuteReader())
				{
					if (reader.Read())
					{
						isAlreadyExpired = Convert.ToByte(reader["IsExpired"]);
						steamID = (ulong)Convert.ToInt64(reader["SteamID"]);

						if (isAlreadyExpired == 1)
						{
							Log.Error($"User {steamID} (CaseID: {caseid}) is already expired.");
							return 1;
						}
					}
					else
					{
						return 2;
					}
				}

				if (steamID != 0)
				{
					Log.Info($"Setting user {steamID} (CaseID: {caseid}) to expired.");

					string updateQuery = @"UPDATE ""bans"" SET ""IsExpired"" = 1 WHERE ""CaseID"" = @caseId;";
					NpgsqlCommand updateCommand = new NpgsqlCommand(updateQuery, connection);
					updateCommand.Parameters.AddWithValue("@caseId", caseid);
					updateCommand.ExecuteNonQuery();

					MyMultiplayerBase.BanUser(steamID, false);
				}
				else
				{
					if (DebugMode)
						Log.Error("No SteamID found for ban with caseId:" + caseid);
					return 2;
				}

				connection.Close();
				return 0;
			}
		}

		public async Task<string> AddUser(ulong steamid, string expireDate, string reason, bool isPermanent)
		{
			string WMPublicAddress = AdvancedBansPatches.WMPublicAddress;

			string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

			string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;

			NpgsqlConnection connection = new NpgsqlConnection(connectionString);
			bool debug = AdvancedBansPatches.Debug;
			try
			{
				connection.Open();
				int permanentVal;
				DateTime MyExpireDate = DateTime.MinValue;
				DateTime MyBannedDate = DateTime.Now;

				if (isPermanent)
				{
					permanentVal = 1;
					expireDate = "";
				}
				else
				{
					permanentVal = 0;
					if (expireDate.EndsWith("m") && int.TryParse(expireDate.Substring(0, expireDate.Length - 1), out int minutes))
					{
						MyExpireDate = DateTime.Now.AddMinutes(minutes);
					}
					else if (expireDate.EndsWith("h") && int.TryParse(expireDate.Substring(0, expireDate.Length - 1), out int hours))
					{
						MyExpireDate = DateTime.Now.AddHours(hours);
					}
					else if (expireDate.EndsWith("d") && int.TryParse(expireDate.Substring(0, expireDate.Length - 1), out int days))
					{
						MyExpireDate = DateTime.Now.AddDays(days);
					}
					else
					{
						Log.Error("Time must be in m, h, or d.");
						return null;
					}
				}

				// Generate a CaseId
				Random random = new Random();
				StringBuilder newCaseId = new StringBuilder();

				for (int i = 0; i < 10; i++)
				{
					int charType = random.Next(3);
					char randomChar;

					if (charType == 0)
					{
						randomChar = (char)random.Next('0', '9' + 1);
					}
					else if (charType == 1)
					{
						randomChar = (char)random.Next('A', 'Z' + 1);
					}
					else
					{
						randomChar = (char)random.Next('a', 'z' + 1);
					}

					newCaseId.Append(randomChar);
				}

				string CaseId = newCaseId.ToString();

				string insertQuery = $@"
                INSERT INTO ""bans"" (""SteamID"", ""CaseID"", ""BannedDate"", ""ExpireDate"", ""Reason"", ""IsPermanent"", ""IsExpired"" )
                VALUES (@steamid, @caseid, @bannedDate, @expireDate, @reason, @isPermanent, 0)
                RETURNING ""CaseID"";";

				using (NpgsqlCommand insertCommand = new NpgsqlCommand(insertQuery, connection))
				{
					insertCommand.Parameters.AddWithValue("@steamid", Convert.ToInt64(steamid));
					insertCommand.Parameters.AddWithValue("@caseid", CaseId);
					insertCommand.Parameters.AddWithValue("@bannedDate", MyBannedDate);
					insertCommand.Parameters.AddWithValue("@expireDate", isPermanent ? DBNull.Value : (object)MyExpireDate);
					insertCommand.Parameters.AddWithValue("@reason", reason);
					insertCommand.Parameters.AddWithValue("@isPermanent", permanentVal);

					CaseId = (string)insertCommand.ExecuteScalar();
				}

				long playerId = MySession.Static.Players.TryGetIdentityId(steamid);
				if (debug)
				{
					Log.Error(playerId.ToString());
					Log.Error("Displaying message");
				}

				string MyUrl = $"http://{WMPublicAddress}:{WebServer.WMPort}/{CaseId}";

				MyVisualScriptLogicProvider.OpenSteamOverlay($"https://steamcommunity.com/linkfilter/?url={MyUrl}", playerId);

				MyVisualScriptLogicProvider.RemoveEntity(playerId);

				MyMultiplayerBase.BanUser(steamid, true);
				if (debug)
				{
					Log.Error($@"
===Ban Info===
SteamId: {steamid}
CaseId: {CaseId}
Banned Date: {MyBannedDate:g}
Expire Date: {MyExpireDate:g}
Is Permanent: {permanentVal}
Is Expired: N/A");
				}

				Log.Info($"User {steamid} (CaseID: {CaseId}) has been banned.");
				await Task.Delay(10000);
				MyMultiplayerBase.BanUser(steamid, false);

				if (debug)
					Log.Info("Database structure initialized.");

				return CaseId;
			}
			catch (NpgsqlException ex)
			{
				Log.Error("Error: " + ex.Message);
				return null;
			}
			finally
			{
				if (connection.State == System.Data.ConnectionState.Open)
				{
					connection.Close();
				}
			}
		}

		/// <summary>
		/// Gets the information of a user in the database based on their SteamID.
		/// </summary>
		/// <param name="steamid"></param>
		/// <returns>Returns a JSON string.</returns>
		public static string GetBannedBySteamID(ulong steamid)
		{
			string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";
			string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;

			var banInfoList = new List<object>();

			using (var connection = new NpgsqlConnection(connectionString))
			{
				connection.Open();

				string query = @"
            SELECT ""SteamID"", ""CaseID"", ""BannedDate"", ""ExpireDate"", ""Reason"", ""IsPermanent"", ""IsExpired""
            FROM ""bans"" WHERE ""SteamID"" = @steamid 
            ORDER BY ""BannedDate"" DESC;";
				using (var command = new NpgsqlCommand(query, connection))
				{
					command.Parameters.AddWithValue("@steamid", (long)steamid);
					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							var banInfo = new
							{
								SteamID = Convert.ToUInt64(reader["SteamID"]),
								CaseID = Convert.ToString(reader["CaseID"]),
								BannedDate = reader["BannedDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["BannedDate"]),
								ExpireDate = reader["ExpireDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ExpireDate"]),
								Reason = Convert.ToString(reader["Reason"]),
								IsPermanentBan = Convert.ToByte(reader["IsPermanent"]),
								IsExpired = Convert.ToByte(reader["IsExpired"])
							};
							banInfoList.Add(banInfo);
						}
					}
				}
			}

			return JsonSerializer.Serialize(banInfoList);
		}


		/// <summary>
		/// Gets the information of a user in the database based on their CaseID.
		/// </summary>
		/// <param name="caseId"></param>
		/// <returns>Returns a JSON string.</returns>
		public static string GetBannedByCaseID(string caseId)
		{
			string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";

			string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;

			var banInfoList = new List<object>();

			NpgsqlConnection connection = new NpgsqlConnection(connectionString);
			try
			{
				connection.Open();

				string insertQuery = @"
                   SELECT ""SteamID"", ""CaseID"", ""BannedDate"", ""ExpireDate"", ""Reason"", ""IsPermanent"", ""IsExpired""
                    FROM ""bans"" WHERE ""CaseID"" = @caseId 
                    ORDER BY ""BannedDate"" DESC LIMIT 1;";

				NpgsqlCommand insertCommand = new NpgsqlCommand(insertQuery, connection);

				insertCommand.Parameters.AddWithValue("@caseId", caseId);
				using (NpgsqlDataReader reader = insertCommand.ExecuteReader())
				{
					if (reader.Read())
					{
						var banInfo = new
						{
							SteamID = Convert.ToUInt64(reader["SteamID"]),
							CaseID = Convert.ToString(reader["CaseID"]),
							BannedDate = reader["BannedDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["BannedDate"]),
							ExpireDate = reader["ExpireDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ExpireDate"]),
							Reason = Convert.ToString(reader["Reason"]),
							IsPermanentBan = Convert.ToByte(reader["IsPermanent"]),
							IsExpired = Convert.ToByte(reader["IsExpired"])
						};

						banInfoList.Add(banInfo);
					}
					else
					{
						Log.Error($"User with caseId: {caseId} cannot be found.");
						return null;
					}

					connection.Close();
					string jsonString = JsonSerializer.Serialize(banInfoList);
					jsonString = jsonString.Replace("'", "\"");
					return jsonString;
				}
			}
			catch (NpgsqlException ex)
			{
				Log.Error("Error: " + ex.Message);
				return null;
			}
		}

		/// <summary>
		/// Checks if isExpired on user is 1. If multiple entries are found, it will return true. Must be used either with GetBannedSteamId or GetBannedCaseId.
		/// </summary>
		/// <param name="jsonString"></param>
		/// <returns>boolean</returns>
		public bool IsBanned(string jsonString)
		{
			var banInfoList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonString);

			foreach (var info in banInfoList)
			{
				byte isExpired = info["IsExpired"].GetByte();
				if (isExpired == 0)
				{
					return true;
				}
			}

			return false;
		}


		/// <summary>
		/// Gets the entire database table, and return it to a json string.
		/// </summary>
		/// <returns></returns>
		public static string AllListed()
		{
			string passwordSegment = DBPassword.Equals("null") ? "" : $"Password={DBPassword};";
			string connectionString = $"Server={DBAddress};Port={DBPort};User ID={DBUser};Database={DBName};" + passwordSegment;

			List<Dictionary<string, object>> bansData = new List<Dictionary<string, object>>();

			using (var connection = new NpgsqlConnection(connectionString))
			{
				connection.Open();
				string selectQuery = "SELECT * FROM bans;";
				using (var selectCommand = new NpgsqlCommand(selectQuery, connection))
				using (var reader = selectCommand.ExecuteReader())
				{
					while (reader.Read())
					{
						Dictionary<string, object> banData = new Dictionary<string, object>();
						for (int i = 0; i < reader.FieldCount; i++)
						{
							banData[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader[i];
						}
						bansData.Add(banData);
					}
				}
			}

			string json = JsonSerializer.Serialize(bansData, new JsonSerializerOptions { WriteIndented = true });
			return json.Replace("'", "\"");
		}
	}
}