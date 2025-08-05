using HarmonyLib;
using NLog;
using Npgsql;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Commands;
using Torch.Mod.Messages;
using Torch.Session;
using VRage.Game.VisualScripting;
using VRage.GameServices;
using static AdvancedBans.WebServer;
using static Sandbox.ModAPI.MyModAPIHelper;

namespace AdvancedBans
{
    public class AdvancedBans : TorchPluginBase, IWpfPlugin
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly string CONFIG_FILE_NAME = "AdvancedBansConfig.cfg";

        private readonly Harmony _harmony = new Harmony("AdvancedBans");
        private AdvancedBansControl _control;
        public UserControl GetControl() => _control ?? (_control = new AdvancedBansControl(this));

        public static AdvancedBans Instance;

        private Persistent<AdvancedBansConfig> _config;
        public AdvancedBansConfig Config => _config?.Data;
        private int TickCount = 0;
        public Database Database;

        public BanRepository banRepo;
        public IPRepository ipRepo;

        public override async void Init(ITorchBase torch)
        {
            base.Init(torch);
            
            SetupConfig();

            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (sessionManager != null)
                sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager loaded!");

            Save();

            if (Config.ShutdownIfNotActive == false)
            {
                Log.Warn("ShutdownIfNotActive is set to false. This means banned players can join if the database cannot be reached. This setting should only be used for initial setup of AdvancedBans. Once set up, enable the setting and restart!");
            }
            Instance = this;

            try
            {
                //Database
                Log.Info("Initializing Database...");
                Database = new Database();

                BanRepository.DebugMode = Config.Debug;
                BanRepository.DebugMode = Config.Debug;
                IPRepository.DebugMode = Config.Debug;

                // then construct them:
                banRepo = new BanRepository(Database);
                ipRepo = new IPRepository(Database);
            }
            catch (NpgsqlException ex)
            {
                if (ex.Message == "Multiple primary key defined")
                { } 
                else
                {
                    if (Config.ShutdownIfNotActive)
                    {
                        Log.Fatal($"Npgsql Error: {ex.Message} Ensure the Postgresql Server is running and try again!\nStackTrace:\n{ex.StackTrace}");
                        ShutdownSafely();
                        return;
                    }
                    Log.Error("WARNING! AdvancedBans could not connect to the database. This means banned players via AdvancedBans can join. Please ensure your database credentials are correct, and enable ShutdownIfNotActive!\n" + ex.Message + "\n" + ex.StackTrace);
                }
                
            }
            catch (Exception e)
            {
                if (Config.ShutdownIfNotActive)
                {
                    Log.Fatal($"Error: Cannot start up AdvancedBans. Check the database credentials. Is {Config.Port} taken?\n" + e);
                    ShutdownSafely();
                    return;
                }
                Log.Error("WARNING! AdvancedBans could not connect to the database. This means banned players via AdvancedBans can join. Please ensure your database credentials are correct, and enable ShutdownIfNotActive!\n" + e.Message + "\n" + e.StackTrace);
            }
            try
            {
                _harmony.PatchAll();
                Log.Info("Methods patched!");
            }
            catch (Exception e)
            {
                Log.Fatal("Error during patch: " + e.Message, e.StackTrace);
                ShutdownSafely();
                return;
            }
            try
            {
                //WebManager
                WebServer MyWebManager = new WebServer(Config.WebAddress, Config.WebPort, Config.WebBanPage, Config.WebErrorPage);
                if (Config.WebEnabled)
                {
                    if (Config.Debug)
                        Log.Warn("Calling WebServer...");
                    await MyWebManager.StartWebServer();
                    if (Config.Debug)
                        Log.Warn("Webserver Called!");
                }
                    

            } catch (Exception e)
            {
                Log.Warn("WebServer could not start. Please check the WebAddress and WebPort in the config file.\n" + e.Message + "\n" + e.StackTrace);
            }


            
        }
        private void ShutdownSafely()
        {
            Torch.Stop();
            Torch.CurrentSession.Torch.Destroy();
            Torch.Destroy();
            return;
        }
        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            var mpMan = Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerServer>();

            switch (state)
            {

                case TorchSessionState.Loaded:
                Log.Info("Session Loaded!");
                if (Config.ShutdownIfNotActive)
                    if (Instance.Database.IsConnected() == false)
                    {
                        Log.Fatal("Database is not connected! Cannot start server!");
                        ShutdownSafely();
                        return;
                    } else
                    {
                        Log.Info("Database is connected! Continuing...");
                    }
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
                    //Register Commands
                    _commandManager.RegisterCommandModule(typeof(AdvancedBansCommands)); // Correct usage of typeof
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

                    try
                    {
                        if (Config.Debug)
                        {
                            Log.Warn("Checking expired bans...");
                        }
                        try
                        {
                            banRepo.CheckAndUnbanExpiredBans();
                        }
                        catch (NpgsqlException ex)
                        {
                            if (ex.Message == "Multiple primary key defined")
                            { }
                        }

                        if (Config.Debug)
                        {
                            Log.Warn("Checking if banned player is online...");
                        }
                    } catch (Exception e)
                    {
                        Log.Error("Failed to check expired bans. (Maybe the database is down)?");
                        if (Config.Debug)
                        {
                            Log.Error(e.Message);
                            Log.Error(e.StackTrace);
                        }
                    }


                    try
					{
						foreach (MyPlayer.PlayerId player in MySession.Static.Players.GetAllPlayers())
						{
							MyPlayerCollection mpc = new MyPlayerCollection();
							if (mpc.IsPlayerOnline(MySession.Static.Players.TryGetIdentityId(player.SteamId))) {

                            
							    var json = banRepo.GetBannedBySteamID(player.SteamId);
                                if (json == null)
                                {
                                    if (Config.Debug)
                                        Log.Error($"Json returned invalid!");
                                    continue;
                                }

							    if (banRepo.IsBanned(json))
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

