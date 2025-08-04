using Torch;

namespace AdvancedBans
{
    public class AdvancedBansConfig : ViewModel
    {
        private bool _Enabled = true;
        private bool _Debug = false;
        private bool _Override = false;
        private bool _LogIPs = false; // Whether to log IP addresses of players, will not log IPs if the server is running on EOS / Crossplay mode.


        private string _DatabaseName = "AdvancedBans";
        private string _LocalAddress = "localhost";
        private int _Port = 5432;
        private string _Username = "postgres";
        private string _Password = "null";
        private int _ScanningInt = 216000; //1 hour
        private int _BanDelay = 10000; //In milliseconds. 15 seconds, this value depends on how much mods you have. If it is a Lobby server, or a lightly modded server, keep it low.
        //Webserver
        private bool _WebEnabled = false;
        private string _WebAddress = "localhost";
        private string _WebPort = "8000";
        private string _WebPublicAddress = "+";
        private string _WebBanPage = @"<!DOCTYPE html>
<html>
<head>
    <meta http-equiv=""cache-control"" content=""no-cache"" />
    <meta http-equiv=""expires"" content=""0"" />
    <meta http-equiv=""expires"" content=""Tue, 01 Jan 1980 1:00:00 GMT"" />
    <meta http-equiv=""pragma"" content=""no-cache"" />
    <title>AdvancedBans</title>
    <style>body {
            font-family: Arial;
            background-color: white;
            color: black;
            }
    </style>
</head>
<body>
    <h1>You have been banned!</h1>
    <p><strong>Steam ID:</strong> {{SteamID}}</p>
    <p><strong>Banned Date:</strong> {{BannedDate}}</p>
    <p><strong>Expire Date:</strong> {{ExpireDate}}</p>
    <p><strong>IsPermanent:</strong> {{IsPermanent}}</p>
    <p><strong>IsExpired:</strong> {{IsExpired}}</p>
    <p><strong>IP Ban:</strong> {{IPBanned}}</p>
    <p><strong>Reason:</strong> {{Reason}}</p>
    <p><strong>Remaining time:</strong> {{RemainingTime}}</p>

    
    <br><p style=""color:red;"" ><strong>Case ID:</strong> {{CaseID}}
    <br>This is your unique Case ID for your ban.
    <br>Sharing your Case ID may affect the processing of your appeal!</p>
    <br><p>Powered by AdvancedBans</p>
    <br><em>This is a sample ban page, consider editing some elements of it!</em>
</body>
</html>";
        private string _WebErrorPage = @"<!DOCTYPE html>
<html>
<head>
    <meta http-equiv=""cache-control"" content=""no-cache"" />
    <meta http-equiv=""expires"" content=""0"" />
    <meta http-equiv=""expires"" content=""Tue, 01 Jan 1980 1:00:00 GMT"" />
    <meta http-equiv=""pragma"" content=""no-cache"" />
    <title>AdvancedBans</title>
    <style>body {
            font-family: Arial;
            }
    </style>
</head>
<body>
    <h1>No ban details provided or ban is invalid.</h1>
</body>
</html>";
        private bool _WebEnforceRateLimits = true;
        private bool _ShutdownIfNotActive = true; // Refuses connections if Database is not active. It will also prevent startup of the server if it is down.
        //Actions
        private bool _AM_BanButton = true;

        private bool _ExperimentalPatches = false;

        //General
        public bool Enabled { get => _Enabled; set => SetValue(ref _Enabled, value); }
        public bool Debug { get => _Debug; set => SetValue(ref _Debug, value); }
        public bool Override { get => _Override; set => SetValue(ref _Override, value); }
        public bool LogIPs { get => _Enabled; set => SetValue(ref _LogIPs, value); }
        public string DatabaseName { get => _DatabaseName; set => SetValue(ref _DatabaseName, value); }
        public string LocalAddress { get => _LocalAddress; set => SetValue(ref _LocalAddress, value); }
        public string Username { get => _Username; set => SetValue(ref _Username, value); }
        public string Password { get => _Password; set => SetValue(ref _Password, value); }
        public int ScanningInt { get => _ScanningInt; set => SetValue(ref _ScanningInt, value); }
        public int Port { get => _Port; set => SetValue(ref _Port, value); }
        public int BanDelay { get => _BanDelay; set => SetValue(ref _BanDelay, value); }
		public bool ExperimentalPatches { get => _ExperimentalPatches; set => SetValue(ref _ExperimentalPatches, value); }
		//WebServer
		public bool WebEnabled { get => _WebEnabled; set => SetValue(ref _WebEnabled, value); }
        public string WebAddress { get => _WebAddress; set => SetValue(ref _WebAddress, value); }
        public string WebPort { get => _WebPort; set => SetValue(ref _WebPort, value); }
        public string WebPublicAddress { get => _WebPublicAddress; set => SetValue(ref _WebPublicAddress, value); }
        public string WebBanPage { get => _WebBanPage; set => SetValue(ref _WebBanPage, value); }
        public string WebErrorPage { get => _WebErrorPage; set => SetValue(ref _WebErrorPage, value); }
		public bool WebEnforceRateLimits { get => _WebEnforceRateLimits; set => SetValue(ref _WebEnforceRateLimits, value); }
        public bool ShutdownIfNotActive { get => _ShutdownIfNotActive; set => SetValue(ref _ShutdownIfNotActive, value); }
        //Actions
        public bool AM_BanButton { get => _AM_BanButton; set => SetValue(ref _AM_BanButton, value); }
    }
}
