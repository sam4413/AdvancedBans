using NLog;
using Npgsql;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AdvancedBans
{
	public class BanRepository
	{
		public static readonly Logger Log = LogManager.GetCurrentClassLogger();

		private Database db;
		internal static bool DebugMode;

		public BanRepository(Database db)
		{
			this.db = db;
		}

		/// <summary>
		/// Checks if any user should be unbanned. It will also check if a user should be banned. If IsPermanent = 1, they will remain banned. If unbanned, their info remains in the database, BUT IsExpired will be 1 instead of 0.
		/// </summary>
		public void CheckAndUnbanExpiredBans()
		{
			string selectQuery = @"SELECT ""SteamID"", ""CaseID"", ""BannedDate"", ""ExpireDate"", ""Reason"", ""IsPermanent"", ""IsExpired""
                            FROM ""bans"";";

			DataTable dtable = db.ExecuteAny(selectQuery); // Fix: Await the Task<DataTable> returned by ExecuteAny

			if (dtable == null || dtable.Rows.Count == 0)
			{
				return;
            }

            foreach (DataRow row in dtable.Rows)
			{
                string SteamIDString = Convert.ToString(row["SteamID"]);
                string CaseID = Convert.ToString(row["CaseID"]);
                string ExpireDateString = row["ExpireDate"] == DBNull.Value ? null : row["ExpireDate"].ToString();
                int isPermanent = Convert.ToByte(row["IsPermanent"]);
                int isExpired = Convert.ToByte(row["IsExpired"]);

				if (DateTime.TryParse(ExpireDateString, out DateTime expireDate))
				{
					if (DateTime.Now > expireDate && isPermanent != 1 && isExpired != 1)
					{
						ulong steamID = ulong.Parse(SteamIDString);

                        var banJson = GetBannedByCaseID(CaseID);
                            if (banJson == null)
								if (DebugMode)
									Log.Error($"Internal error. User {steamID} (CaseID: {CaseID}) could not be unbanned.");

                        Log.Info($"User {steamID} (CaseID: {CaseID}) ban has expired.");
                        int result = SetExpired(CaseID);
						if (DebugMode)
						{
                            Log.Error(result);
                        }
                    }
				}
            }
		}




        /// <summary>
        /// Sets a ban's status to expired based on caseId, unbanning the user. Success = 0, Already Expired = 1, Not found = 2
        /// </summary>
        /// <param name="caseid"></param>
        /// <returns>An integer 0-2</returns>
        public int SetExpired(string caseid)
        {
            // 1) Look up the ban record by CaseID
            const string selectSql = @"
        SELECT ""IsExpired"", ""SteamID""
        FROM ""bans""
        WHERE ""CaseID"" = @caseid;";
            var selectParams = new Dictionary<string, object>
            {
                ["@caseid"] = caseid
            };

            var table = db.ExecuteAny(selectSql, selectParams);
            if (table == null || table.Rows.Count == 0)
            {
                // no such ban
                return 2;
            }

            // take the first (and only) row
            var row = table.Rows[0];
            bool alreadyExpired = Convert.ToByte(row["IsExpired"]) == 1;
            ulong steamID = (ulong)Convert.ToInt64(row["SteamID"]);

            if (alreadyExpired)
            {
                // it’s already marked expired
                return 1;
            }

            // 2) mark it expired
            const string updateSql = @"
        UPDATE ""bans""
        SET ""IsExpired"" = 1
        WHERE ""CaseID"" = @caseid;";
            var updateParams = new Dictionary<string, object>
            {
                ["@caseid"] = caseid
            };
            db.ExecuteAny(updateSql, updateParams);

            // 3) kick off the unban in‐game
            MyMultiplayerBase.BanUser(steamID, false);

            // success
            return 0;
        }



        public async Task<string> AddUser(ulong steamid, string expireDate, string reason, bool isPermanent, bool isIPEnforced)
        {
            string WMPublicAddress = AdvancedBans.Instance.Config.WebPublicAddress;

            string passwordSegment = AdvancedBans.Instance.Config.Password.Equals("null") ? "" : $"Password={AdvancedBans.Instance.Config.Password};";

            string connectionString = $"Server={AdvancedBans.Instance.Config.LocalAddress};Port={AdvancedBans.Instance.Config.Port};User ID={AdvancedBans.Instance.Config.Username};Database={AdvancedBans.Instance.Config.DatabaseName};" + passwordSegment;

            NpgsqlConnection connection = new NpgsqlConnection(connectionString);
            bool debug = AdvancedBans.Instance.Config.Debug;
            try
            {
                connection.Open();
                int permanentVal = 0;
                int ipVal = 0;
                DateTime MyExpireDate = DateTime.MinValue;
                DateTime MyBannedDate = DateTime.Now;

                if (isPermanent)
                {
                    permanentVal = 1;
                    expireDate = "";
                }

                if (isIPEnforced)
                    ipVal = 1;
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
                INSERT INTO ""bans"" (""SteamID"", ""CaseID"", ""BannedDate"", ""ExpireDate"", ""Reason"", ""IsPermanent"", ""IsExpired"", ""EnforceIP"" )
                VALUES (@steamid, @caseid, @bannedDate, @expireDate, @reason, @isPermanent, 0, @ipEnforce)
                RETURNING ""CaseID"";";

                using (NpgsqlCommand insertCommand = new NpgsqlCommand(insertQuery, connection))
                {
                    insertCommand.Parameters.AddWithValue("@steamid", Convert.ToInt64(steamid));
                    insertCommand.Parameters.AddWithValue("@caseid", CaseId);
                    insertCommand.Parameters.AddWithValue("@bannedDate", MyBannedDate);
                    insertCommand.Parameters.AddWithValue("@expireDate", isPermanent ? DBNull.Value : (object)MyExpireDate);
                    insertCommand.Parameters.AddWithValue("@reason", reason);
                    insertCommand.Parameters.AddWithValue("@isPermanent", permanentVal);
                    insertCommand.Parameters.AddWithValue("@ipEnforce", ipVal);

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
Is Expired: N/A
EnforceIP: {ipVal}");
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
        public string GetBannedBySteamID(ulong steamid)
		{
			var banInfoList = new List<object>();


			string query = @"
            SELECT ""SteamID"", ""CaseID"", ""BannedDate"", ""ExpireDate"", ""Reason"", ""IsPermanent"", ""IsExpired"", ""EnforceIP""
            FROM ""bans"" WHERE ""SteamID"" = @steamid 
            ORDER BY ""BannedDate"" DESC;";

			var parameters = new Dictionary<string, object>
				{
					{ "@steamid", (long)steamid }
				};

			DataTable dtable = db.ExecuteAny(query, parameters);
			if (dtable == null || dtable.Rows.Count == 0)
			{
				return null;
            }

            foreach (DataRow row in dtable.Rows)
			{
				var banInfo = new
				{
					SteamID = Convert.ToUInt64(row["SteamID"]),
					CaseID = Convert.ToString(row["CaseID"]),
					BannedDate = row["BannedDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["BannedDate"]),
					ExpireDate = row["ExpireDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ExpireDate"]),
					Reason = Convert.ToString(row["Reason"]),
					IsPermanentBan = Convert.ToByte(row["IsPermanent"]),
					IsExpired = Convert.ToByte(row["IsExpired"]),
                    EnforceIP = Convert.ToByte(row["IsExpired"])

                };
				banInfoList.Add(banInfo);
			}

			return JsonSerializer.Serialize(banInfoList);
		}


		/// <summary>
		/// Gets the information of a user in the database based on their CaseID.
		/// </summary>
		/// <param name="caseId"></param>
		/// <returns>Returns a JSON string.</returns>
		public string GetBannedByCaseID(string caseId)
		{
			var banInfoList = new List<object>();
			try
			{

				string insertQuery = @"
                   SELECT ""SteamID"", ""CaseID"", ""BannedDate"", ""ExpireDate"", ""Reason"", ""IsPermanent"", ""IsExpired"", ""EnforceIP""
                    FROM ""bans"" WHERE ""CaseID"" = @caseId 
                    ORDER BY ""BannedDate"" DESC LIMIT 1;";

                var parameters = new Dictionary<string, object>
                {
                    { "@caseId", caseId }
                };

				DataTable dtable = db.ExecuteAny(insertQuery, parameters); // Fix: Await the Task<DataTable> returned by ExecuteAny

				if (dtable == null || dtable.Rows.Count == 0)
				{
					if (AdvancedBans.Instance.Config.Debug)
						Log.Error($"User with caseId: {caseId} cannot be found.");
					return null;
                }

                foreach (DataRow row in dtable.Rows)
				{
					var banInfo = new
					{
						SteamID = Convert.ToUInt64(row["SteamID"]),
						CaseID = Convert.ToString(row["CaseID"]),
						BannedDate = row["BannedDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["BannedDate"]),
						ExpireDate = row["ExpireDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ExpireDate"]),
						Reason = Convert.ToString(row["Reason"]),
						IsPermanentBan = Convert.ToByte(row["IsPermanent"]),
						IsExpired = Convert.ToByte(row["IsExpired"]),
						EnforceIP = Convert.ToByte(row["EnforceIP"])
                    };

					banInfoList.Add(banInfo);
				}

				if (banInfoList.Count == 0)
				{
					Log.Error($"User with caseId: {caseId} cannot be found.");
					return null;
				}

				string jsonString = JsonSerializer.Serialize(banInfoList);
				jsonString = jsonString.Replace("'", "\"");
				return jsonString;
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

			if (banInfoList == null || banInfoList.Count == 0)
			{
				return false; // No bans found
            }

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
		public string AllListed()
        {
            string selectQuery = "SELECT * FROM bans;";
            DataTable dtable = db.ExecuteAny(selectQuery); // Fix: Await the Task<DataTable> returned by ExecuteAny

			if (dtable == null || dtable.Rows.Count == 0)
			{
				return "[]"; // Return an empty JSON array if no bans are found
            }

            List<Dictionary<string, object>> bansData = new List<Dictionary<string, object>>();

            foreach (DataRow row in dtable.Rows)
            {
                Dictionary<string, object> banData = new Dictionary<string, object>();
                foreach (DataColumn column in dtable.Columns)
                {
                    banData[column.ColumnName] = row[column] == DBNull.Value ? null : row[column];
                }
                bansData.Add(banData);	
            }

            string json = JsonSerializer.Serialize(bansData, new JsonSerializerOptions { WriteIndented = true });
            return json.Replace("'", "\"");
        }
	}
}