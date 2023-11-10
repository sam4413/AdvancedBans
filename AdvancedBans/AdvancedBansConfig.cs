using System;
using System.Collections.Generic;
using Torch;

namespace AdvancedBans
{
    public class AdvancedBansConfig : ViewModel
    {
        private bool _Enabled = true;

        private string _DatabaseName = "AdvancedBans";
        private string _LocalAddress = "localhost";
        private int _Port = 3306;
        private string _Username = "root";
        private int _ScanningInt = 72000; //1 hour
        //private string _Password = "";

        public string DatabaseName { get => _DatabaseName; set => SetValue(ref _DatabaseName, value); }
        public string LocalAddress { get => _LocalAddress; set => SetValue(ref _LocalAddress, value); }
        public string Username { get => _Username; set => SetValue(ref _Username, value); }
        public int ScanningInt { get => _ScanningInt; set => SetValue(ref _ScanningInt, value); }
        public int Port { get => _Port; set => SetValue(ref _Port, value); }
        public bool Enabled { get => _Enabled; set => SetValue(ref _Enabled, value); }
    }
}
