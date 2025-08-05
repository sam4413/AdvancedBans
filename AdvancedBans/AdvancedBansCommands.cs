using NLog;
using Npgsql;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Server.Managers;
using VRage.Game;
using VRage;
using VRage.Game.ModAPI;

namespace AdvancedBans
{
    /// <summary>
    /// todo: redo commands and add help for once
    /// </summary>
    [Category("ab")]
    public class AdvancedBansCommands : CommandModule
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();


        public AdvancedBans Plugin => (AdvancedBans)Context.Plugin;

        [Command("help", "View help for AdvancedBans.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Help()
        {

            if (Context.Player != null)
            {
                Context.Respond($"", VRageMath.Color.PaleVioletRed, "=== AdvancedBans Help ===");
                Context.Respond($"Perm-ban a user from the server and ban their IP from joining.", VRageMath.Color.LimeGreen, "!ab permban \"<name/SteamID>\" \"<reason>\"");
                Context.Respond($"Perm-ban a user from the server and ban their IP from joining.", VRageMath.Color.LimeGreen, "!ab permbanip \"<name/SteamID>\" \"<reason>\"");
                //Context.Respond($"Blacklist a user from the server by banning their SteamID, as well as any of their alts.", VRageMath.Color.LimeGreen, "!ab blacklist \"<name/SteamID>\" \"<reason>\"");
                Context.Respond($"Temp-ban a user from the server for a certain amount of time.", VRageMath.Color.LimeGreen, "!ab tempban \"<name/SteamID>\" \"<time>\" \"<reason>\"");
                Context.Respond($"Temp-ban a user from the server for a certain amount of time and ban their IP from joining.", VRageMath.Color.LimeGreen, "!ab tempbanip \"<name/SteamID>\" \"<time>\" \"<reason>\"");
                Context.Respond($"View the ban history for a specific user or Steam ID.", VRageMath.Color.LimeGreen, "!ab history \"<name/SteamID>\"");
                Context.Respond($"Unban a user based on their Case ID or Steam ID.", VRageMath.Color.LimeGreen, "!ab unban \"<CaseID/SteamID>\"");
                Context.Respond($"Lists any alts associated with the SteamID or Given IP address. Will only list steam players.", VRageMath.Color.LimeGreen, "!ab alts \"<SteamID/IP>\"");
                Context.Respond($"", VRageMath.Color.SlateBlue, $"=== AdvancedBans {Plugin.Version} ===");
                return;
            }
            else
            {
                Context.Respond(
                "=== AdvancedBans Help ===\n" +
                "!ab help - Shows this message\n" +
                "!ab permban \"<name/SteamID>\" \"<reason>\" - Perm-ban a user from the server.\n" +
                "!ab permbanip \"<name/SteamID>\" \"<reason>\" - Perm-ban a user from the server and ban their IP from joining.\n" +
                //"!ab blacklist \"<name/SteamID>\" \"<reason>\" - Blacklist a user from the server by banning their SteamID, as well as any of their alts.\n" +
                "!ab tempban \"<name/SteamID>\" \"<time>\" \"<reason>\" - Temp-ban a user from the server for a certain amount of time.\n" +
                "!ab tempbanip \"<name/SteamID>\" \"<time>\" \"<reason>\" - Temp-ban a user from the server for a certain amount of time and ban their IP from joining.\n" +
                "!ab history \"<name/SteamID>\" - View the ban history for a specific user or Steam ID.\n" +
                "!ab unban \"<CaseID/SteamID>\" - Unban a user based on their Case ID or Steam ID.\n" +
                "!ab alts \"<SteamID/IP>\" - Lists any alts associated with the SteamID or Given IP address. Will only list steam players.\n" +
                $"=== AdvancedBans {Plugin.Version} ==="
                );
                return;
            }
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
                    string CaseId = await Plugin.banRepo.AddUser(id, date, reason, false, false);

                    if (string.IsNullOrEmpty(CaseId) || CaseId == null)
                    {
                        Context.Respond($"Error: Failed to tempban player with Steam ID {steamId}. Please check your syntax and try again.");
                        return;
                    }

                    Context.Respond($"Success: Player {identity.DisplayName} tempbanned. ({id})\nCase ID: {CaseId}");
                    return;
                }
            }

            if (isId)
            {
                string CaseId = Plugin.banRepo.AddUser(steamId, date, reason, false, false).Result;
                Context.Respond($"Success: Player tempbanned. ({steamId})\nCase ID: {CaseId}");
                return;
            }

            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }
        [Command("tempbanip", "Tempban a user, as well as their IP.")]
        [Permission(MyPromoteLevel.Admin)]
        public async void AB_TempBanIP(string nameOrSteamId, string date, string reason)
        {

            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            if (isId)
            {
                string CaseId = await Plugin.banRepo.AddUser(steamId, "", reason, false, true);
                Context.Respond($"Player permabanned. ({steamId})\nCase ID: {CaseId}");
                return;
            }
            else
            {
                foreach (var identity in MySession.Static.Players.GetAllIdentities())
                {

                    var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                    if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                    {
                        string CaseId = Plugin.banRepo.AddUser(id, "", reason, false, true).Result;

                        if (!string.IsNullOrEmpty(CaseId))
                        {
                            Context.Respond($"Success: Player {identity.DisplayName} permabanned. ({id})\nCase ID: {CaseId}");
                            return;
                        }
                        return;
                    }
                }
            }

            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }


        [Command("permban", "Permaban a user.")]
        [Permission(MyPromoteLevel.Admin)]
        public async void AB_Ban(string nameOrSteamId, string reason)
        {
            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            if (isId)
            {
                string CaseId = Plugin.banRepo.AddUser(steamId, "", reason, true, false).Result;
                Context.Respond($"Player permabanned. ({steamId})\nCase ID: {CaseId}");
                return;
            }
            else
            {
                foreach (var identity in MySession.Static.Players.GetAllIdentities())
                {

                    var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                    if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                    {
                        string CaseId = await Plugin.banRepo.AddUser(id, "", reason, true, false);

                        if (!string.IsNullOrEmpty(CaseId))
                        {
                            Context.Respond($"Success: Player {identity.DisplayName} permabanned. ({id})\nCase ID: {CaseId}");
                            return;
                        }
                        return;
                    }
                }
            }

            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }

        [Command("permbanip", "Permaban a user's steam ID, as well as their IP.")]
        [Permission(MyPromoteLevel.Admin)]
        public async void AB_BanIP(string nameOrSteamId, string reason)
        {
            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            if (isId)
            {
                string CaseId = Plugin.banRepo.AddUser(steamId, "", reason, true, true).Result;
                Context.Respond($"Player permabanned. ({steamId})\nCase ID: {CaseId}");
                return;
            } else
            {
                foreach (var identity in MySession.Static.Players.GetAllIdentities())
                {

                    var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                    if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                    {
                        string CaseId = await Plugin.banRepo.AddUser(id, "", reason, true, true);

                        if (!string.IsNullOrEmpty(CaseId))
                        {
                            Context.Respond($"Success: Player {identity.DisplayName} permabanned. ({id})\nCase ID: {CaseId}");
                            return;
                        }
                        return;
                    }
                }
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
                string banInfo = Plugin.banRepo.GetBannedBySteamID(steamId);
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

                Plugin.banRepo.SetExpired(caseId);
                Context.Respond($"Success: User {steamId} (Case ID: {caseId}) is unbanned.");
            }
            else
            {
                // Unban based on CaseID
                string caseId = args;
                int res = Plugin.banRepo.SetExpired(caseId);
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
                        string jsonData = Plugin.banRepo.GetBannedBySteamID(id);
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

                            if (Context.Player != null)
                            {
                                Context.Respond($"", VRageMath.Color.Blue, $"=== Ban #{i} ===");
                                Context.Respond($"{dbCaseId}", VRageMath.Color.LightGreen, $"Case ID");
                                Context.Respond($"{(bannedDate.HasValue ? bannedDate.Value.ToString("MM-dd-yyyy") : "Unknown")}", VRageMath.Color.Gold, "Date Banned");
                                Context.Respond($"{(isPermanentBan == 1 ? "Never (Permanent)" : (expireDate.HasValue ? expireDate.Value.ToString("MM-dd-yyyy") : "Unknown"))}", VRageMath.Color.Gold, "Expire Date");
                                Context.Respond($"{reason}", VRageMath.Color.Gold, "Reason");
                                Context.Respond($"{(isExpired == 1 ? "Expired" : "Active")}", VRageMath.Color.Gold, "Status");
                                Context.Respond($"{remainingTime}", VRageMath.Color.Gold, "Remaining Time");
                            }
                            else
                            {
                                Context.Respond($@"
                                    === Ban #{i} ===
                                    Case ID: {dbCaseId}
                                    Date Banned: {(bannedDate.HasValue ? bannedDate.Value.ToString("MM-dd-yyyy") : "Unknown")}
                                    Expire Date: {(isPermanentBan == 1 ? "Never (Permanent)" : (expireDate.HasValue ? expireDate.Value.ToString("MM-dd-yyyy") : "Unknown"))}
                                    Reason: {reason}
                                    Status: {(isExpired == 1 ? "Expired" : "Active")}
                                    Remaining Time: {remainingTime} 
                                    ");
                            }

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

        [Command("alts", "View any associated accounts based on the given SteamID or IP Address.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AB_Alts(string steamIdOrIP)
        {
            try
            {


                // If it's a SteamID, check by SteamID. If it's an IP, check by IP. If it's neither, return an error message.
                bool isId = ulong.TryParse(steamIdOrIP, out var steamId);
                if (isId)
                {
                    // Check by SteamID
                    var alts = AdvancedBans.Instance.ipRepo.GetIPsBySteamID(steamId);
                    if (alts == null || alts.Count == 0)
                    {
                        Context.Respond($"No alternate accounts found for Steam ID: {steamId}");
                        return;
                    }
                    Context.Respond($"Alternate accounts for Steam ID: {steamId}");
                    int count = 1;
                    foreach (var alt in alts)
                    {
                        if (Context.Player != null)
                        {
                            Context.Respond($"", VRageMath.Color.Blue, $"=== Alternate Account #{count} ===");
                            Context.Respond($"{alt["Address"]}", VRageMath.Color.LightGreen, "IP Address");
                            Context.Respond($"{alt["LastKnownDate"]}", VRageMath.Color.Gold, "Last Known Date");
                        }
                        else
                            Context.Respond($"=== Alternate Account #{count}\nSteamID: {steamId}, IP Address: {alt["Address"]}, Last Known Date: {alt["LastKnownDate"]}");

                        count++;
                    }
                }
                else
                {
                    // Try to parse as IP address (IPv4 or IPv6, with or without port)
                    IPAddress addr = null;
                    bool validIp = false;
                    validIp = IPAddress.TryParse(steamIdOrIP, out addr);
                    if (AdvancedBans.Instance.Config.Debug)
                        Log.Warn(addr);
                    // Check by IP Address
                    if (validIp)
                    {
                        var alts = AdvancedBans.Instance.ipRepo.GetAltAccountsByIP(steamIdOrIP);
                        if (alts == null || alts.Count == 0)
                        {
                            Context.Respond($"No alternate accounts found for IP Address: {steamIdOrIP}");
                            return;
                        }
                        Context.Respond($"Alternate accounts for IP Address: {steamIdOrIP}");
                        int count = 1;
                        foreach (var alt in alts)
                        {
                            if (Context.Player != null)
                            {
                                Context.Respond($"", VRageMath.Color.Blue, $"=== Alternate Account #{count} ===");
                                Context.Respond($"{alt.Item1}", VRageMath.Color.LightGreen, "Steam ID");
                                Context.Respond($"{alt.Item2}", VRageMath.Color.LightGreen, "IP Address");
                                Context.Respond($"{alt.Item3}", VRageMath.Color.Gold, "Last Known Date");
                            }
                            else
                                Context.Respond($"=== Alternate Account #{count}\nSteamID: {alt.Item1}, IP Address: {alt.Item2}, Last Known Date: {alt.Item3}");

                            count++;
                        }
                    }
                    else
                    {
                        Context.Respond($"This IP / SteamID cannot be parsed. Please try again.");
                    }
                }
            }
            catch (Exception)
            {
                Context.Respond($"This IP / SteamID cannot be parsed. Please try again.");
            }
        }
    }
}
