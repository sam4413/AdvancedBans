using System;
using System.Collections.Generic;
using Torch;

namespace AdvancedBans
{
    public class AdvancedBansConfig : ViewModel
    {
        private bool _Enabled = true;
        private bool _Debug = false;
        private bool _Override = false;

        private string _DatabaseName = "AdvancedBans";
        private string _LocalAddress = "localhost";
        private int _Port = 3306;
        private string _Username = "root";
        private string _Password = "null";
        private int _ScanningInt = 216000; //1 hour
        private int _BanDelay = 15000; //In milliseconds. 15 seconds, this value depends on how much mods you have. If it is a Lobby server, or a lightly modded server, keep it low.
        //Webserver
        private bool _WebEnabled = true;
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
    <p><strong>Ban Number:</strong> {{BanNumber}}</p>
    <p><strong>SteamID:</strong> {{SteamID}}</p>
    <p><strong>ExpireDate:</strong> {{ExpireDate}}</p>
    <p><strong>Reason:</strong> {{Reason}}</p>
    <br><p>Powered by AdvancedBans</p>
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
        //Actions
        private bool _AM_BanButton = false;
        private string _CM_BanPlayer = "Player {{user}} ({{userId}}) banned. Ban number: {{banNumber}}";
        private string _CM_TempbanPlayer = "Player {{user}} ({{userId}}) tempbanned. Ban will expire in {{time}}. Ban number: {{banNumber}}";
        private string _CM_UnbanPlayer = "Player {{user}} ({{userId}}) with ban number {{banNumber}} unbanned.";
        private string _CM_HelpMenu = @"AdvancedBans - v.1.0
Help coming soon.";

        //General
        public bool Enabled { get => _Enabled; set => SetValue(ref _Enabled, value); }
        public bool Debug { get => _Debug; set => SetValue(ref _Debug, value); }
        public bool Override { get => _Override; set => SetValue(ref _Override, value); }
        public string DatabaseName { get => _DatabaseName; set => SetValue(ref _DatabaseName, value); }
        public string LocalAddress { get => _LocalAddress; set => SetValue(ref _LocalAddress, value); }
        public string Username { get => _Username; set => SetValue(ref _Username, value); }
        public string Password { get => _Password; set => SetValue(ref _Password, value); }
        public int ScanningInt { get => _ScanningInt; set => SetValue(ref _ScanningInt, value); }
        public int Port { get => _Port; set => SetValue(ref _Port, value); }
        public int BanDelay { get => _BanDelay; set => SetValue(ref _BanDelay, value); }
        //WebServer
        public bool WebEnabled { get => _WebEnabled; set => SetValue(ref _WebEnabled, value); }
        public string WebAddress { get => _WebAddress; set => SetValue(ref _WebAddress, value); }
        public string WebPort { get => _WebPort; set => SetValue(ref _WebPort, value); }
        public string WebPublicAddress { get => _WebPublicAddress; set => SetValue(ref _WebPublicAddress, value); }
        public string WebBanPage { get => _WebBanPage; set => SetValue(ref _WebBanPage, value); }
        public string WebErrorPage { get => _WebErrorPage; set => SetValue(ref _WebErrorPage, value); }

        //Actions
        public bool AM_BanButton { get => _AM_BanButton; set => SetValue(ref _AM_BanButton, value); }
        public string CM_BanPlayer { get => _CM_BanPlayer; set => SetValue(ref _CM_BanPlayer, value); }
        public string CM_TempbanPlayer { get => _CM_TempbanPlayer; set => SetValue(ref _CM_TempbanPlayer, value); }
        public string CM_UnbanPlayer { get => _CM_UnbanPlayer; set => SetValue(ref _CM_UnbanPlayer, value); }
        public string CM_HelpMenu { get => _CM_HelpMenu; set => SetValue(ref _CM_HelpMenu, value); }
    }
}
