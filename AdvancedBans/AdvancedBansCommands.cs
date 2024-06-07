using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using NLog;
using System.Text.Json;
using System.Linq;
using Npgsql;
using System.Collections.Generic;
using VRage.Game;
using System;
using System.Web;
using AdvancedBans.AdvancedBans;

namespace AdvancedBans
{
    /// <summary>
    /// todo: redo commands and add help for once
    /// </summary>
    [Category("ab")]
    public class AdvancedBansCommands : CommandModule
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        Database MyDatabase = new Database(Database.DBName, Database.DBAddress, Database.DBPort, Database.DBUser, Database.DBPassword);

        public AdvancedBansPlugin Plugin => (AdvancedBansPlugin)Context.Plugin;

        [Command("help", "View help for AdvancedBans.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Test()
        {
            Context.Respond("Help will be added soon. Please view the video tutorial or wiki.");
        }

        [Command("tempban", "Tempban a user.")]
        [Permission(MyPromoteLevel.Admin)]
        public async void AB_TempBan(string nameOrSteamId, string date, string reason)
        {

            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                {
                    string CaseId = await MyDatabase.AddUser(id, date, reason, false);
                    Context.Respond($"Success: Player {identity.DisplayName} tempbanned. ({id})\nCase ID: {CaseId}");
                    return;
                }
            }

            if (isId)
            {
                string CaseId = await MyDatabase.AddUser(steamId, date, reason, false);
                Context.Respond($"Success: Player tempbanned. ({steamId})\nCase ID: {CaseId}");
                return;
            }

            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }


        [Command("ban", "Permaban user.")]
        [Permission(MyPromoteLevel.Admin)]
        public async void AB_Ban(string nameOrSteamId, string reason)
        {
            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                {
                    string CaseId = await MyDatabase.AddUser(id, "", reason, true);
                    Context.Respond($"Success: Player {identity.DisplayName} permabanned. ({id})\nCase ID: {CaseId}");
                    return;
                }
            }

            if (isId)
            {
                string CaseId = await MyDatabase.AddUser(steamId, "", reason, true);
                Context.Respond($"Player permabanned. ({steamId})\nCase ID: {CaseId}");
                return;
            }

            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }
		[Command("unban", "Unban user based on caseid or steamid.")]
		[Permission(MyPromoteLevel.Admin)]
		public void AB_UnBan(string args)
		{
			if (ulong.TryParse(args, out var steamId))
			{
				// Unban based on SteamID
				string banInfo = Database.GetBannedBySteamID(steamId);
				if (string.IsNullOrEmpty(banInfo))
				{
					Context.Respond($"Error: No ban records found for Steam ID: {steamId}.");
					return;
				}

				var banInfoList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(banInfo);
				if (banInfoList == null || banInfoList.Count == 0)
				{
					Context.Respond($"Error: No ban records found for Steam ID: {steamId}.");
					return;
				}

				string caseId = banInfoList[0]["CaseID"].GetString();

				Database.SetExpired(caseId);
				Context.Respond($"Success: User {steamId} (Case ID: {caseId}) is unbanned.");
			}
			else
			{
				// Unban based on CaseID
				string caseId = args;
				int res = Database.SetExpired(caseId);
				if (res == 0)
				{
					Context.Respond($"Success: User with Case ID: {caseId} is unbanned.");
				}
				else if (res == 1)
				{
					Context.Respond($"Error: User with Case ID: {caseId} is already unbanned.");
				}
				else
				{
					Context.Respond($"Error: Cannot find a user with a Case ID of {caseId}. Please try again.");
				}
			}
		}

		[Command("history", "View history for a specific user / steamid.")]
		[Permission(MyPromoteLevel.Admin)]
		public void AB_History(string nameOrSteamId)
		{
			var isId = ulong.TryParse(nameOrSteamId, out var steamId);

			foreach (var identity in MySession.Static.Players.GetAllIdentities())
			{
				var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
				if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
				{
					// Handle potential database connection issues and data retrieval
					try
					{
						string jsonData = Database.GetBannedBySteamID(id);
						if (string.IsNullOrEmpty(jsonData))
						{
							Context.Respond($"No ban history found for: {nameOrSteamId}");
							return;
						}

						var banInfoList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonData);

						Context.Respond($"Ban history for: {nameOrSteamId}");
						int i = 1;
						foreach (var banInfo in banInfoList)
						{
							ulong steamid = banInfo["SteamID"].GetUInt64();
							string dbCaseId = banInfo["CaseID"].GetString();
							DateTime? bannedDate = banInfo.ContainsKey("BannedDate") && banInfo["BannedDate"].ValueKind != JsonValueKind.Null ? (DateTime?)banInfo["BannedDate"].GetDateTime() : null;
							DateTime? expireDate = banInfo.ContainsKey("ExpireDate") && banInfo["ExpireDate"].ValueKind != JsonValueKind.Null ? (DateTime?)banInfo["ExpireDate"].GetDateTime() : null;
							string reason = banInfo["Reason"].GetString();
							byte isPermanentBan = banInfo["IsPermanentBan"].GetByte();
							byte isExpired = banInfo["IsExpired"].GetByte();

							string remainingTime = isPermanentBan == 1 ? "N/A" : (expireDate.HasValue ? WebServer.FormatTimeSpan(expireDate.Value - DateTime.Now) : "Unknown");

							Context.Respond($"", VRageMath.Color.Blue, $"=== Ban #{i} ===");
							Context.Respond($"{dbCaseId}", VRageMath.Color.LightGreen, $"Case ID");
							Context.Respond($"{(bannedDate.HasValue ? bannedDate.Value.ToString("MM-dd-yyyy") : "Unknown")}", VRageMath.Color.Gold, "Date Banned");
							Context.Respond($"{(isPermanentBan == 1 ? "Never (Permanent)" : (expireDate.HasValue ? expireDate.Value.ToString("MM-dd-yyyy") : "Unknown"))}", VRageMath.Color.Gold, "Expire Date");
							Context.Respond($"{reason}", VRageMath.Color.Gold, "Reason");
							Context.Respond($"{(isExpired == 1 ? "Expired" : "Active")}", VRageMath.Color.Gold, "Status");
							Context.Respond($"{remainingTime}", VRageMath.Color.Gold, "Remaining Time");
							i++;
						}
					}
					catch (Exception ex)
					{
						Context.Respond($"Error retrieving ban history: {ex.Message}");
						Log.Error(ex.ToString());
					}
					return;
				}
			}

			Context.Respond($"No user found with the name or SteamID: {nameOrSteamId}");
		}
		//var title = jsonObject.SelectToken("$.book.title").ToString();
	}

    /*
    [Category("advancedbans")]
    public class AdvancedBansHelp : CommandModule
    {

        public AdvancedBansPlugin Plugin => (AdvancedBansPlugin)Context.Plugin;

        [Command("help", "View help for AdvancedBans.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Test()
        {
            Context.Respond("insert help here");
        }
    }*/
}
