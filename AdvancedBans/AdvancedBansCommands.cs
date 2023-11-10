using Sandbox.Game;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace AdvancedBans
{
    [Category("ab")]
    public class AdvancedBansCommands : CommandModule
    {
        Database MyDatabase = new Database(Database.DBName, Database.DBAddress, Database.DBPort, Database.DBUser);

        public AdvancedBans Plugin => (AdvancedBans)Context.Plugin;

        [Command("help", "View help for AdvancedBans.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Test()
        {
            Context.Respond("insert help here");
        }

        [Command("tempban", "Tempban a user.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AB_TempBan(ulong steamid, string date, string reason )
        {
            MyDatabase.AddUser(steamid, date, reason, false);
            Context.Respond("child tempbanned "+steamid);
            Context.Respond($"The child will be unbanned at {date}");
        }

        [Command("ban", "This is a Test Command.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AB_Ban(ulong steamid, string reason)
        {
            MyDatabase.AddUser(steamid, "", reason, true);
            Context.Respond("child banned " + steamid);
        }
        [Command("unban", "This is a Test Command.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AB_UnBan(int BanNumber)
        {
            MyDatabase.RemoveUser(BanNumber);
            Context.Respond("child unbanned " + BanNumber);
        }

    }

    [Category("advancedbans")]
    public class AdvancedBansHelp : CommandModule
    {

        public AdvancedBans Plugin => (AdvancedBans)Context.Plugin;

        [Command("help", "View help for AdvancedBans.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Test()
        {
            Context.Respond("insert help here");
        }
    }
}
