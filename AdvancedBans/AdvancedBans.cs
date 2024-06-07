using NLog;
using Sandbox.Engine.Multiplayer;
using System;
using System.IO;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;
using Npgsql;
using AdvancedBans.AdvancedBans;
using HarmonyLib;
using System.Collections.Generic;
using Torch.Commands;
using System.Threading.Tasks;
using static Sandbox.ModAPI.MyModAPIHelper;
using VRage.GameServices;
using Sandbox.Game.World;
using VRage.Game.VisualScripting;
using System.Text.Json;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;

namespace AdvancedBans
{
    public class BanInfo
    {
        public bool Success { get; set; }
        public string BanNumber { get; set; }
        public ulong SteamID { get; set; }
        public string Reason { get; set; }
        public DateTime ExpireDate { get; set; }
        public bool IsPermanent { get; set; }
        public bool IsExpired { get; set; }
    }
    public class AdvancedBansPlugin : TorchPluginBase, IWpfPlugin
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly string CONFIG_FILE_NAME = "AdvancedBansConfig.cfg";

        private readonly Harmony _harmony = new Harmony("AdvancedBans.AdvancedBansPlugin");

        private AdvancedBansControl _control;
        public UserControl GetControl() => _control ?? (_control = new AdvancedBansControl(this));

        public static AdvancedBansPlugin Instance;

        private Persistent<AdvancedBansConfig> _config;
        public AdvancedBansConfig Config => _config?.Data;
        private int TickCount = 0;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            SetupConfig();

            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (sessionManager != null)
                sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager loaded!");

            Save();
            //WebManager
            WebServer MyWebManager = new WebServer(Config.WebAddress, Config.WebPort, Config.WebBanPage, Config.WebErrorPage);
            if (Config.WebEnabled)
                WebServer.StartWebServer();
                AdvancedBansPatches ConfigValues = new AdvancedBansPatches(Config.WebPublicAddress,Config.DatabaseName,Config.LocalAddress,Config.Port,Config.Username,Config.Password,Config.Debug);
            
            try
            {
                Database.DebugMode = Config.Debug;
                AdvancedBansPatches.BanDelay = Config.BanDelay;
                Database MyDatabase = new Database(Config.DatabaseName, Config.LocalAddress, Config.Port, Config.Username, Config.Password);
                MyDatabase.CreateDatabase();
                MyDatabase.CreateDatabaseStructure();
            }
            catch (NpgsqlException ex)
            {
                if (ex.Message == "Multiple primary key defined")
                { } 
                else
                {
                    Log.Fatal($"Npgsql Error: {ex.Message} Ensure the Postgresql Server is running and try again!\nStackTrace:\n{ex.StackTrace}");
                    Torch.Destroy();
                }
                
            }
            catch (Exception e)
            {
                Log.Fatal($"Error: Cannot start up AdvancedBans. Is {Config.Port} taken?\n" + e);
                Torch.Destroy();
            }

            try
            {
                _harmony.PatchAll();
            } catch (Exception e)
            {
                Log.Fatal("Error during patch: " + e.Message,e.StackTrace);
				Torch.Destroy();
			}
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            var mpMan = Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerServer>();

			switch (state)
            {

                case TorchSessionState.Loaded:
                    Log.Info("Session Loaded!");
                    mpMan.PlayerBanned += IMultiplayerManagerServer_PlayerBanned;
                    break;

                case TorchSessionState.Unloading:
                    Log.Info("Session Unloading!");
                    break;
            }
        }

        private void GameStateChanged(ITorchSession session, TorchGameState state)
        {
            var _commandManager = Torch.CurrentSession.Managers.GetManager<CommandManager>();

            switch (state)
            {

                case TorchGameState.Created:
                    if (Config.Override) // TODO: Make this work
                    {
                        _commandManager.Commands.GetNode(new List<string> { "ban" }, out var banNode);
                        _commandManager.Commands.DeleteNode(banNode);
                        _commandManager.RegisterCommandModule(typeof(AdvancedBansCommands));
                    }
                    Log.Info("State Created!");
                    break;
            }
        }

        private async void IMultiplayerManagerServer_PlayerBanned(ulong steamid, bool banned)
        {
            if (Config.AM_BanButton)
            {
                if (banned == true)
                {
                    await Task.Delay(1000);
                    MyMultiplayerBase.BanUser(steamid, false);
                }
            }
        }

        private void SetupConfig()
        {

            var configFile = Path.Combine(StoragePath, CONFIG_FILE_NAME);

            try
            {

                _config = Persistent<AdvancedBansConfig>.Load(configFile);

            }
            catch (Exception e)
            {
                Log.Warn(e);
            }

            if (_config?.Data == null)
            {

                Log.Info("Create Default Config, because none was found!");

                _config = new Persistent<AdvancedBansConfig>(configFile, new AdvancedBansConfig());
                _config.Save();
            }
        }

        public void Save()
        {
            try
            {
                _config.Save();
                Log.Info("Configuration Saved.");
            }
            catch (IOException e)
            {
                Log.Warn(e, "Configuration failed to save");
            }
        }
		public override async void Update()
		{
			base.Update();
			if (Config.Enabled)
			{
				if (TickCount >= Config.ScanningInt)
				{
					TickCount = 0;
					string DBUser = Config.Username;
					string DBPassword = Config.Password;
					string DBAddress = Config.LocalAddress;
					string DBName = Config.DatabaseName;
					int DBPort = Config.Port;

					Database MyDatabase = new Database(DBName, DBAddress, DBPort, DBUser, DBPassword);
					if (Config.Debug)
					{
						Log.Warn("Checking expired bans...");
					}
					try
					{
						MyDatabase.CheckAndUnbanExpiredBans();
					}
					catch (NpgsqlException ex)
					{
						if (ex.Message == "Multiple primary key defined")
						{ }
					}
					catch (Exception e)
					{
						Log.Error(e.Message);
						Log.Error(e.StackTrace);
					}

					if (Config.Debug)
					{
						Log.Warn("Checking if banned player is online...");
					}
					try
					{
						foreach (MyPlayer.PlayerId player in MySession.Static.Players.GetAllPlayers())
						{
							MyPlayerCollection mpc = new MyPlayerCollection();
							if (mpc.IsPlayerOnline(MySession.Static.Players.TryGetIdentityId(player.SteamId))) {

                            
							    var json = Database.GetBannedBySteamID(player.SteamId);
							    if (MyDatabase.IsBanned(json))
							    {
								    Log.Info($"Banned player ({player.SteamId}) detected. Showing message.");

								    // Define the anonymous type structure
								    var options = new JsonSerializerOptions
								    {
									    PropertyNameCaseInsensitive = true
								    };

								    // Parse the JSON data into a list of anonymous objects
								    var banInfoList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, options);

								    // Access the first entry in the list
								    if (banInfoList != null && banInfoList.Count > 0)
								    {
									    var firstBanInfo = banInfoList[0];

									    if (firstBanInfo.TryGetValue("CaseID", out JsonElement caseIdElement))
									    {
										    string caseId = caseIdElement.GetString();
										    WebServer.ShowBanMessage($"/{caseId}", MySession.Static.Players.TryGetIdentityId(player.SteamId));
                                            MyMultiplayerBase.BanUser(player.SteamId, true);

                                            await Task.Delay(10000);

                                            MyMultiplayerBase.BanUser(player.SteamId, false);
									    }
								    }
							    }
							}
							else
							{
								if (Config.Debug)
								{
									Log.Info($"Player {player.SteamId} not banned.");
								}
							}
						}
					}
					catch (NpgsqlException ex)
					{
						if (ex.Message == "Multiple primary key defined")
						{ }
					}
					catch (Exception e)
					{
						Log.Error(e.Message);
						Log.Error(e.StackTrace);
					}
				}
				else
				{
					TickCount++;
				}
			}
		}
	}
}

