using AdvancedBans.AdvancedBans;
using Sandbox.Game;
using Sandbox.Game.World;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using NLog;
using Sandbox.Game.Entities;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using MySqlConnector;

namespace AdvancedBans
{
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
            Context.Respond("insert help here");
        }

        [Command("tempban", "Tempban a user.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AB_TempBan(string nameOrSteamId, string date, string reason )
        {
            nameOrSteamId = MySqlHelper.EscapeString(nameOrSteamId);
            date = MySqlHelper.EscapeString(date);
            reason = MySqlHelper.EscapeString(reason);

            var isId = ulong.TryParse(nameOrSteamId, out var steamId);


            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                {
                    
                    int MyBanNumber = MyDatabase.AddUser(id, date, reason, false);
                    
                    Context.Respond($"Player {identity.DisplayName} tempbanned. ({id})\nBan Number: {MyBanNumber}");
                    return;
                }
            }

            if (isId)
            {
                int MyBanNumber = MyDatabase.AddUser(steamId, date, reason, false);
                Context.Respond($"Player tempbanned. ({steamId})\nBan Number: {MyBanNumber}");
                return;
            }

            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }

        [Command("ban", "Permaban user.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AB_Ban(string nameOrSteamId, string reason)
        {

            nameOrSteamId = MySqlHelper.EscapeString(nameOrSteamId);
            reason = MySqlHelper.EscapeString(reason);

            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                {
                    int MyBanNumber = MyDatabase.AddUser(id, "", reason, true);
                    Context.Respond($"Player {identity.DisplayName} permabanned. ({id})\nBan Number: {MyBanNumber}");
                    return;
                }
            }

            if (isId)
            {
                int MyBanNumber = MyDatabase.AddUser(steamId, "", reason, true);
                Context.Respond($"Player permabanned. ({steamId})\nBan Number: {MyBanNumber}");
                return;
            }

            Context.Respond($"Player '{nameOrSteamId}' not found.");
        }
        [Command("unban", "Unban user.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AB_UnBan(string BanNumber)
        {
            BanNumber = MySqlHelper.EscapeString(BanNumber);

            var isParsed = int.TryParse(BanNumber, out int safeBanNumber);
            if (isParsed)
                Database.SetExpired(safeBanNumber);
                Context.Respond("Player unbanned " + BanNumber);

        }

        [Command("history", "View history for a specific user / steamid.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AB_History(string nameOrSteamId)
        {
            nameOrSteamId = MySqlHelper.EscapeString(nameOrSteamId);

            var isId = ulong.TryParse(nameOrSteamId, out var steamId);

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                var id = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                if (id != 0 && (identity.DisplayName == nameOrSteamId || id == steamId))
                {
                    string JSONRes = Database.AllListed();
                    //Log.Warn(JSONRes);

                    var jsonArray = JArray.Parse(JSONRes);
                    long specificSteamId = (long)id;

                    var filteredResults = jsonArray
                        .Where(jt => (long)jt["SteamID"] == specificSteamId)
                        .ToList();
                    foreach (var result in filteredResults)
                    {
                        //Log.Error(result.ToString());
                        Context.Respond(result.ToString());
                    }
                    return;
                }
            }

            if (isId)
            {
                string JSONRes = Database.AllListed();
                Log.Warn(JSONRes);

                var jsonArray = JArray.Parse(JSONRes);
                var specificSteamId = steamId;

                var filteredResults = jsonArray
                    .Where(jt => (long)jt["SteamID"] == (long)specificSteamId)
                    .ToList();
                foreach (var result in filteredResults)
                {
                    Log.Error(result.ToString());
                    Context.Respond(result.ToString());
                }
                return;
            }

            Context.Respond($"Player '{nameOrSteamId}' not found.");

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
