using System;
using System.Threading.Tasks;
using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using VRage.Network;
using NLog;
using Sandbox.Game;
using Sandbox.Game.World;
using System.Collections.Generic;
using System.Text.Json;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.Multiplayer;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.GameServices;

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

		public static AdvancedBansConfig Config = new AdvancedBansConfig();

		public async static void CheckIfBannedUser(ulong obj)
		{
			try
			{
				if (Debug)
					Log.Error(obj);

				AdvancedBansConfig Config = new AdvancedBansConfig();
				Database MyDatabase = new Database(DBName, DBAddress, DBPort, DBUser, DBPassword);

				string banInfo = Database.GetBannedBySteamID(obj);
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
					if (Debug)
					{
						Log.Error($"Banned player {obj} detected. Showing message");
					}

					var MyPlayerId = MySession.Static.Players.TryGetIdentityId(obj);
					WebServer.ShowBanMessage(Config.WebPublicAddress + $"/{caseId}", MyPlayerId);

					await Task.Delay(BanDelay);
					MyVisualScriptLogicProvider.RemoveEntity(MyPlayerId);
					MyMultiplayerBase.BanUser(obj, true);
					await Task.Delay(10000);
					MyMultiplayerBase.BanUser(obj, false);

					Log.Info("User removed.");
				}
				else
				{
					Log.Info($"User {obj} is NOT banned.");
					if (Debug)
					{
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
				CheckIfBannedUser(MyPatchData);

				return true;
			}
		}

		[HarmonyPatch(typeof(MySessionComponentMatch))]
		[HarmonyPatch("OnPlayerSpawned")]
		class Patch2
		{
			private static bool Prefix(ref long playerId)
			{
				if (Config.ExperimentalPatches)
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
				if (Config.ExperimentalPatches)
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
				if (Config.ExperimentalPatches)
				{
					Log.Error(response);
					return true;
				}
				return true;
			}
		}
	}
}