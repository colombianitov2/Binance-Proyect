using System.Windows;
using System.Windows.Controls;

namespace CryptoSelfBot.Wpf.Components.Header
{
    public partial class HeaderControl : UserControl
    {
        public event RoutedEventHandler? NotificationsClicked;

        public HeaderControl()
        {
            InitializeComponent();
        }

        private void NotificationToggle_Click(object sender, RoutedEventArgs e)
        {
            NotificationsClicked?.Invoke(this, e);
        }
    }
}
