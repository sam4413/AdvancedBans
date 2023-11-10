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
using MySql.Data.MySqlClient;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;

namespace AdvancedBans
{
    public class AdvancedBans : TorchPluginBase, IWpfPlugin
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly string CONFIG_FILE_NAME = "AdvancedBansConfig.cfg";

        private AdvancedBansControl _control;
        public UserControl GetControl() => _control ?? (_control = new AdvancedBansControl(this));

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
            //Time for some funky DB Stuff

            string DBUser = Config.Username;
            string DBAddress = Config.LocalAddress;
            string DBName = Config.DatabaseName;
            int DBPort = Config.Port;

            try
            {
                Database MyDatabase = new Database(DBName, DBAddress, DBPort, DBUser);
                MyDatabase.CreateDatabase();
                MyDatabase.CreateDatabaseStructure();
            }
            catch (Exception e)
            {
                Log.Fatal($"Cannot start up AdvancedBans. Is {Config.Port} taken?\n" + e);
            }
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {

            switch (state)
            {

                case TorchSessionState.Loaded:
                    Log.Info("Session Loaded!");
                    break;

                case TorchSessionState.Unloading:
                    Log.Info("Session Unloading!");
                    break;
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
        public override void Update()
        {
            base.Update();
            if (Config.Enabled)
            {
                if (TickCount >= Config.ScanningInt)
                {

                    TickCount = 0;
                    string DBUser = Config.Username;
                    string DBAddress = Config.LocalAddress;
                    string DBName = Config.DatabaseName;
                    int DBPort = Config.Port;
                    Database MyDatabase = new Database(DBName, DBAddress, DBPort, DBUser);
                    Log.Warn("Checking expired bans...");
                    MyDatabase.CheckAndUnbanExpiredBans();
                } else {
                    TickCount++;
                }
            }
        }
    }
}

