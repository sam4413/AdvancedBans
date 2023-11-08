using System.Windows;
using System.Windows.Controls;

namespace AdvancedBans
{
    public partial class AdvancedBansControl : UserControl
    {

        private AdvancedBans Plugin { get; }

        private AdvancedBansControl()
        {
            InitializeComponent();
        }

        public AdvancedBansControl(AdvancedBans plugin) : this()
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
