using System.Windows;
using System.Windows.Controls;

namespace CryptoSelfBot.Wpf.Components.Menu
{
    public partial class MenuControl : UserControl
    {
        public MenuControl()
        {
            InitializeComponent();
        }

        private void OnHistoryClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(HistoryClickedEvent));
        private void OnNewsClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(NewsClickedEvent));
        private void OnExportClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ExportClickedEvent));
        private void OnSourcesClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SourcesClickedEvent));
        private void OnWithdrawClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(WithdrawClickedEvent));
        private void OnSettingsClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SettingsClickedEvent));
        private void OnInstructionsClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(InstructionsClickedEvent));
        private void OnCreditsClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CreditsClickedEvent));

        public static readonly RoutedEvent HistoryClickedEvent = EventManager.RegisterRoutedEvent("HistoryClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuControl));
        public static readonly RoutedEvent NewsClickedEvent = EventManager.RegisterRoutedEvent("NewsClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuControl));
        public static readonly RoutedEvent ExportClickedEvent = EventManager.RegisterRoutedEvent("ExportClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuControl));
        public static readonly RoutedEvent SourcesClickedEvent = EventManager.RegisterRoutedEvent("SourcesClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuControl));
        public static readonly RoutedEvent WithdrawClickedEvent = EventManager.RegisterRoutedEvent("WithdrawClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuControl));
        public static readonly RoutedEvent SettingsClickedEvent = EventManager.RegisterRoutedEvent("SettingsClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuControl));
        public static readonly RoutedEvent InstructionsClickedEvent = EventManager.RegisterRoutedEvent("InstructionsClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuControl));
        public static readonly RoutedEvent CreditsClickedEvent = EventManager.RegisterRoutedEvent("CreditsClicked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuControl));
    }
}
