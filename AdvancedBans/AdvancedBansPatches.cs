using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using VRage.Network;
using NLog;
using Sandbox.Game;
using Torch.API;
using Sandbox.Game.World;
using System.Net.Http;

namespace AdvancedBans.AdvancedBans
{
    class AdvancedBansPatches
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static string DBName;
        public static string DBUser;
        public static string DBAddress;
        public static int DBPort;
        internal static string DBPassword;

        public static string WMPublicAddress;

        public static int BanDelay;
        public static bool Debug;
        public AdvancedBansPatches(string pa, string name, string address, int port, string username, string password, bool isDebug)
        {
            WMPublicAddress = pa;
            DBName = name;
            DBAddress = address;
            DBPort = port;
            DBUser = username;
            DBPassword = password;
            Debug = isDebug;
        }

        public async static void CheckIfBannedUser(ulong obj)
        {
            try
            {
                if (Debug == true)
                    Log.Error(obj);
                Database MyDatabase = new Database(DBName, DBAddress, DBPort, DBUser, DBPassword);
                Log.Error(MyDatabase.IsBanned(obj));
                if (MyDatabase.IsBanned(obj) == true)
                {
                    Log.Info($"User {obj} is banned.");
                    if (Debug == true)
                    {
                        Log.Error($"Banned player {obj} detected. Showing message");
                        Log.Error($@"
    ===Ban Info===
    Ban Number: {Database.LatestBanNumber}
    SteamId: {Database.LatestBanSteamID}
    Expire Date: {Database.LatestBanExpireDate.ToString("g")}
    Is Permanent: {Database.LatestIsPermanentBan}
    Is Expired: {Database.LatestIsExpired}
    ");
                    }  
                    string MyUrl = $"http://{WMPublicAddress}:{WebServer.WMPort}/?BanNumber={Database.LatestBanNumber}&SteamID={Database.LatestBanSteamID}&ExpireDate={Database.LatestBanExpireDate.ToString("g")}&IsPermanent={Database.LatestIsPermanentBan}&Reason={Database.LatestBanReason}";

                    var MyPlayerId = MySession.Static.Players.TryGetIdentityId((ulong)Database.LatestBanSteamID);
                    await Task.Delay(BanDelay);
                    MyVisualScriptLogicProvider.OpenSteamOverlay($"https://steamcommunity.com/linkfilter/?url={MyUrl}", MyPlayerId);

                    MyMultiplayerBase.BanUser(obj, true);
                    if (AdvancedBansPlugin.Instance.Config.AM_BanButton == false)
                    {
                        await Task.Delay(1000);
                        MyMultiplayerBase.BanUser(obj, false);
                    }
                    Log.Info("User removed.");
                }
                else
                {
                    if (Debug == true)
                    {
                        Log.Error($@"
    ===Ban Info===
    Ban Number: {Database.LatestBanNumber}
    SteamId: {Database.LatestBanSteamID}
    Expire Date: {Database.LatestBanExpireDate.ToString("g")}
    Is Permanent: {Database.LatestIsPermanentBan}
    Is Expired: {Database.LatestIsExpired}
    ");
                        Log.Error("Player is not banned!");
                    } 
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
        [HarmonyPatch(typeof(MyMultiplayerServerBase))]
        [HarmonyPatch("OnWorldRequest")]
        class Patch
        {
            protected static bool Prefix(ref EndpointId sender)
            {
                var MyPatchData = sender.Value;
                Log.Error(MyPatchData);
                CheckIfBannedUser(MyPatchData);

                return true;
            }
        }
    }
}