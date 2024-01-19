using System.Windows;
using System.Windows.Controls;

namespace AdvancedBans
{
    public partial class AdvancedBansControl : UserControl
    {

        private AdvancedBansPlugin Plugin { get; }

        private AdvancedBansControl()
        {
            InitializeComponent();
        }

        public AdvancedBansControl(AdvancedBansPlugin plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
        }
    }
}