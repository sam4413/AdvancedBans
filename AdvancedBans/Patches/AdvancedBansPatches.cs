using HarmonyLib;
using NLog;
using Npgsql;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Game;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Torch.Server.Managers;
using VRage.GameServices;
using VRage.Network;
using VRage.ObjectBuilders;
using VRageMath;

namespace AdvancedBans
{
    class AdvancedBansPatches
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Check if the user is banned and handle the ban accordingly. If address is null, it will not enforce the ban but will still check if the user is banned via steam id
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="address"></param>
        /// <param name="enforce"></param>
        public static void CheckIfBannedUser(ulong obj, IPAddress address)
        {
            if (AdvancedBans.Instance.Config.LogIPs == true && address != null)
            {
                if (AdvancedBans.Instance.Config.Debug)
                    Log.Error($"Checking if user {obj} with IP {address} is banned.");

                //AdvancedBansConfig Config = new AdvancedBansConfig();


                bool isIPBanned = AdvancedBans.Instance.ipRepo.IsIPBanned(address);
                if (!isIPBanned)
                {
                    Log.Info($"IP {address} is not banned.");
                    // If the user is not banned by IP, check if they are banned by Steam ID
                    string banInfo = AdvancedBans.Instance.banRepo.GetBannedBySteamID(obj);
                    if (string.IsNullOrEmpty(banInfo))
                    {
                        Log.Info($"User {obj} has no ban records.");
                        return;
                    }
                    else
                    {
                        HandleBanLogic(obj);
                        return;
                    }
                }
                else
                {
                    Log.Info($"User {obj} with IP {address} is banned!");
                    //Their IP is banned, handle the ban
                    HandleBanLogic(obj);
                    return;
                }




            }
            else
            {
                try
                {
                    if (AdvancedBans.Instance.Config.Debug)
                        Log.Error(obj + " addr: " + address);

                    //AdvancedBansConfig Config = new AdvancedBansConfig();

                    HandleBanLogic(obj);
                    return;
                }
                catch (Exception e)
                {
                    Log.Error($"Error checking if user {obj} is banned: {e.Message}");
                    MyVisualScriptLogicProvider.DisconnectPlayer(obj); //Disconnect the player if an error occurs
                }
            }
        }

        private static async void HandleBanLogic(ulong obj)
        {
            string banInfo = AdvancedBans.Instance.banRepo.GetBannedBySteamID(obj);
            if (string.IsNullOrEmpty(banInfo))
            {
                Log.Info($"User {obj} has no ban records.");
                return;
            }

            var banInfoList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(banInfo);

            string caseId = "";
            bool isBanned = false;

            foreach (var info in banInfoList)
            {
                if (info.ContainsKey("CaseID") && info.ContainsKey("IsExpired") && info["IsExpired"].GetByte() == 0)
                {
                    caseId = info["CaseID"].GetString();
                    isBanned = true;
                    break;
                }
            }

            if (isBanned)
            {
                Log.Info($"User {obj} is banned.");
                if (AdvancedBans.Instance.Config.Debug)
                {
                    Log.Error($"Banned player {obj} detected. Showing message");
                }

                var MyPlayerId = MySession.Static.Players.TryGetIdentityId(obj);
                WebServer.ShowBanMessage(AdvancedBans.Instance.Config.WebPublicAddress + $"/{caseId}", MyPlayerId);

                if (AdvancedBans.Instance.Config.BanDelay <= 0)
                {
                    MyVisualScriptLogicProvider.RemoveEntity(MyPlayerId);
                    MyMultiplayerBase.BanUser(obj, true);
                    await Task.Delay(10000);
                    MyMultiplayerBase.BanUser(obj, false);
                }
                else
                {
                    await Task.Delay(AdvancedBans.Instance.Config.BanDelay);
                    MyVisualScriptLogicProvider.RemoveEntity(MyPlayerId);
                    MyMultiplayerBase.BanUser(obj, true);
                    await Task.Delay(10000);
                    MyMultiplayerBase.BanUser(obj, false);
                }


                Log.Info("User removed.");
            }
            else
            {
                Log.Info($"User {obj} is NOT banned.");
                if (AdvancedBans.Instance.Config.Debug)
                {
                    Log.Error("Player is not banned!");
                }
            }
        }

        [HarmonyPatch(typeof(MyMultiplayerServerBase))]
        [HarmonyPatch("OnWorldRequest")]
        class Patch
        {
            protected static bool Prefix(EndpointId sender)
            {
                var senderVal = sender.Value;
                try
                {
                    if (AdvancedBans.Instance.Config.LogIPs)
                    {
                        if (MyPlatformGameSettings.CONSOLE_COMPATIBLE == false)
                        {
                            MyP2PSessionState state = default(MyP2PSessionState);
                            MyGameService.Peer2Peer.GetSessionState(sender.Value, ref state);
                            IPAddress arg = new IPAddress(BitConverter.GetBytes(state.RemoteIP).Reverse().ToArray());
                            if (arg.ToString().Equals("0.0.0.0"))
                            {
                                CheckIfBannedUser(senderVal, null);
                                return true;
                            }
                            AdvancedBans.Instance.ipRepo.AddOrUpdateIPEntry(senderVal, arg);
                            CheckIfBannedUser(senderVal, arg);
                            return true;
                        }
                        else
                        {
                            Log.Warn("Console compatability is enabled! IP Enforcement does not work for console servers. Please disable the Log and Enforce IPs option to hide this message!");
                            CheckIfBannedUser(senderVal, null);
                            return true;
                        }
                    }
                    CheckIfBannedUser(senderVal, null); // We don't have the IP address here, so we pass null for now.
                    return true;
                }
                catch (NpgsqlException e)
                {
                    if (AdvancedBans.Instance.Config.ShutdownIfNotActive)
                    {
                        Log.Error($"The database cannot be reached. Connections will be refused until the database is reachable.\nError: {e.Message}\nStackTrace: {e.StackTrace}");
                        MyVisualScriptLogicProvider.DisconnectPlayer(senderVal); // Disconnect the player if an error occurs
                        return false;
                    }
                    Log.Error($"The database cannot be reached. Cannot vertify the validity of the player.\nError: {e.Message}\nStackTrace: {e.StackTrace}");
                    return true; // If the database is not reachable, we refuse the connection
                }
                catch (Exception e)
                {
                    if (AdvancedBans.Instance.Config.ShutdownIfNotActive)
                    {
                        Log.Error($"An unhandled error has occured. For security, connections will be refused.\nError: {e.Message}\nStackTrace: {e.StackTrace}");
                        MyVisualScriptLogicProvider.DisconnectPlayer(senderVal); // Disconnect the player if an error occurs
                        return false;
                    }
                    Log.Error($"An unhandled error has occured. Cannot vertify the validity of the player.\nError: {e.Message}\nStackTrace: {e.StackTrace}");
                    return true;
                }
            }

            [HarmonyPatch(typeof(MySessionComponentMatch))]
            [HarmonyPatch("OnPlayerSpawned")]
            class Patch2
            {
                private static bool Prefix(ref long playerId)
                {
                    if (AdvancedBans.Instance.Config.ExperimentalPatches)
                    {
                        Log.Error("Player spawned: " + playerId);
                        Log.Error(MyVisualScriptLogicProvider.GetSteamId(playerId));
                        return true;
                    }
                    return true;
                }
            }

            [HarmonyPatch(typeof(MyPlayerCollection))]
            [HarmonyPatch(nameof(MyPlayerCollection.OnRespawnRequest),
                new Type[] {
            typeof(bool), typeof(bool), typeof(long), typeof(string), typeof(Vector3D?),
            typeof(Vector3?), typeof(Vector3?), typeof(SerializableDefinitionId?),
            typeof(bool), typeof(int), typeof(string), typeof(Color)
                }
            )]
            class Patch3
            {
                private static bool Prefix(
                    ref bool joinGame,
                    bool newIdentity,
                    long respawnEntityId,
                    string respawnShipId,
                    Vector3D? spawnPosition,
                    Vector3? direction,
                    Vector3? up,
                    SerializableDefinitionId? botDefinitionId,
                    bool realPlayer,
                    int playerSerialId,
                    string modelName,
                    Color color)
                {
                    if (AdvancedBans.Instance.Config.ExperimentalPatches)
                    {
                        Log.Error("Respawn Request: " + respawnEntityId);
                        Log.Error(MyVisualScriptLogicProvider.GetSteamId(playerSerialId));

                        Log.Error("---");
                        Log.Error("Joining game?: " + joinGame);
                        Log.Error("Real Player?: " + realPlayer);
                        return true;
                    }
                    return true;
                }
            }
            [HarmonyPatch(typeof(MyMultiplayerJoinResult))]
            [HarmonyPatch("RaiseJoined")]
            class Patch4
            {
                private static bool Prefix(ref
                    bool success,
                    IMyLobby lobby,
                    MyLobbyStatusCode response,
                    MyMultiplayerBase multiplayer
                )
                {
                    if (AdvancedBans.Instance.Config.ExperimentalPatches)
                    {
                        Log.Error(response);
                        return true;
                    }
                    return true;
                }
            }
        }
    }
}