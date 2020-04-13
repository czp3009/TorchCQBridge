using System.Windows;

namespace TorchCQBridge
{
    public partial class TorchCqBridgeControl
    {
        private readonly TorchCqBridgePlugin _plugin;

        private TorchCqBridgeControl()
        {
            InitializeComponent();
        }

        public TorchCqBridgeControl(TorchCqBridgePlugin plugin) : this()
        {
            _plugin = plugin;
            DataContext = plugin.Config;
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            _plugin.Save();
        }

        private void RestartButton_OnClick(object sender, RoutedEventArgs e)
        {
            _plugin.RestartBot();
        }
    }
}