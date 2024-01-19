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

        private void TextBox_TextChanged()
        {

        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void CheckBox_Checked_1(object sender, RoutedEventArgs e)
        {

        }

        private void CheckBox_Checked_2(object sender, RoutedEventArgs e)
        {

        }

        private void CheckBox_Checked_3(object sender, RoutedEventArgs e)
        {

        }
    }
}
