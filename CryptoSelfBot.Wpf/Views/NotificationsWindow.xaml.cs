using System.Windows;

namespace CryptoSelfBot.Wpf.Views
{
    public partial class NotificationsWindow : Window
    {
        public NotificationsWindow()
        {
            InitializeComponent();
            DataContext = Application.Current.MainWindow?.DataContext;
        }
    }
}
