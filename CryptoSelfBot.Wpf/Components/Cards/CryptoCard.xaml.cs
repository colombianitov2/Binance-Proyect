using System.Windows;
using System.Windows.Controls;

namespace CryptoSelfBot.Wpf.Components.Cards
{
    public partial class CryptoCard : UserControl
    {
        public CryptoCard()
        {
            InitializeComponent();
        }

        public static readonly RoutedEvent RemoveClickedEvent = EventManager.RegisterRoutedEvent(
            "RemoveClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CryptoCard));

        private void OnRemoveClicked(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(RemoveClickedEvent, this));
        }
    }
}
