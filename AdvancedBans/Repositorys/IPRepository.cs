using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Threading.Tasks;

namespace AdvancedBans
{
	public class IPRepository
	{
        private Database db;
        internal static bool DebugMode;

        public IPRepository(Database db)
        {
            this.db = db;
        }

        /// <summary>
		/// Add an IP to the database for a specific SteamID.
		/// </summary>
		public void AddOrUpdateIPEntry(ulong steamid, IPAddress address)
        {

            //First lets SELECT all the IPs from the database where the SteamID matches the one we want to add or update.

            string selectQuery = @"SELECT ""Address"", ""LastKnownDate"" FROM ""ip"" WHERE ""SteamID"" = @SteamID;";

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@SteamID", (long)steamid } // Assuming you want to use the local player's SteamID
            };

            DataTable dtable = db.ExecuteAny(selectQuery, parameters); // Fix: Await the Task<DataTable> returned by ExecuteAny

            if (dtable == null)
            {
                // We know that the table is empty, so lets insert a new player
                string insertQuery = @"INSERT INTO ""ip"" (""SteamID"", ""Address"", ""LastKnownDate"") VALUES (@SteamID, @Address, CURRENT_TIMESTAMP);";
                Dictionary<string, object> insertParameters = new Dictionary<string, object>
                {
                    { "@SteamID", (long)steamid },
                    { "@Address", address.ToString() } // Convert IPAddress to string
                };

                db.ExecuteAny(insertQuery, insertParameters);
            }
            else
            {
                // We now know the table is not empty. This means the player IP was logged. This means that we need to update the last known date of the player IP.
                string updateQuery = @"UPDATE ""ip"" SET ""LastKnownDate"" = CURRENT_TIMESTAMP WHERE ""SteamID"" = @SteamID;";
                Dictionary<string, object> updateParameters = new Dictionary<string, object>
                {
                    { "@SteamID", (long)steamid }
                };
                db.ExecuteAny(updateQuery, updateParameters);
            }
        }

        /// <summary>
        /// Gets a list of all IP addresses associated with a specific SteamID. Returns a list of dictionaries, where each dictionary contains the Address and LastKnownDate of the IPs.
        /// </summary>
        /// <param name="steamid"></param>
        /// <returns></returns>
        public List<Dictionary<string, string>> GetIPsBySteamID(ulong steamid)
        {
            string query = @"SELECT ""Address"", ""LastKnownDate"" FROM ""ip"" WHERE ""SteamID"" = @SteamID;";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@SteamID", (long)steamid }
            };
            DataTable result = db.ExecuteAny(query, parameters);
            List<Dictionary<string, string>> ipList = new List<Dictionary<string, string>>();
            foreach (DataRow row in result.Rows)
            {
                var ipEntry = new Dictionary<string, string>
                {
                    { "Address", row["Address"].ToString() },
                    { "LastKnownDate", row["LastKnownDate"].ToString() }
                };
                ipList.Add(ipEntry);
            }
            return ipList;
        }

        /// <summary>
        /// Gets the last known IP address for a specific SteamID. Returns the IP address as a string.
        /// </summary>
        /// <param name="steamid"></param>
        /// <returns></returns>
        public string GetLastKnownIP(ulong steamid)
        {
            string query = @"SELECT ""Address"" FROM ""ip"" WHERE ""SteamID"" = @SteamID ORDER BY ""LastKnownDate"" DESC LIMIT 1;";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@SteamID", (long)steamid }
            };
            DataTable result = db.ExecuteAny(query, parameters);
            if (result.Rows.Count > 0)
            {
                return result.Rows[0]["Address"].ToString();
            }
            return null; // No IP found for the given SteamID
        }

        /// <summary>
        /// Gets a list of all alternate accounts associated with a specific IP address. Returns a list of ValueTuples of strings, where each tuple contains the SteamID, Address, and LastKnownDate of the alternate accounts.
        /// </summary>
        /// <param name="address"></param>
        /// <returns>Row1: SteamID, Row2: Address, Row3: LastKnownDate</returns>
        public List<(string, string, string)> GetAltAccountsByIP(string address)
        {
            string query = @"SELECT ""SteamID"", ""Address"", ""LastKnownDate"" FROM ""ip"" WHERE ""Address"" = @Address;";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@Address", address } // Convert IPAddress to string
            };
            DataTable result = db.ExecuteAny(query, parameters);
            if (result == null || result.Rows.Count == 0)
            {
                return new List<ValueTuple<string, string, string>>(); // No alternate accounts found
            }
            List<ValueTuple<string, string, string>> altAccounts = new List<ValueTuple<string, string, string>>();
            foreach (DataRow row in result.Rows)
            {
                var accountEntry = new ValueTuple<string, string, string>
                (
                    row["SteamID"].ToString(),
                    row["Address"].ToString(),
                    row["LastKnownDate"].ToString()
                );
                altAccounts.Add(accountEntry);
            }
            return altAccounts;
        }

        /// <summary>
        /// Checks if a specific IP address is banned. Returns true if the IP is banned, false otherwise.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool IsIPBanned(IPAddress address)
        {
            string query = @"SELECT COUNT(*) 
                             FROM ""bans"" b
                             INNER JOIN ""ip"" i ON b.""SteamID"" = i.""SteamID""
                             WHERE b.""EnforceIP"" = 1 
                               AND b.""IsExpired"" = 0 
                               AND i.""Address"" = @Address;";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@Address", address.ToString() } // Convert IPAddress to string
            };
            DataTable result = db.ExecuteAny(query, parameters);
            if (result.Rows.Count > 0 && Convert.ToInt32(result.Rows[0][0]) > 0)
            {
                return true; // IP is banned
            }
            return false; // IP is not banned
        }
    }
}