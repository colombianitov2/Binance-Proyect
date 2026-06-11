using CryptoSelfBot.Engine.Services;
using System.Windows;

namespace CryptoSelfBot.Wpf.Views
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            CargarHistorial();
            this.Activated += HistoryWindow_Activated;
        }

        private async void CargarHistorial()
        {
            var dbService = new DatabaseService();
            await dbService.InitializeAsync();
            var trades = await dbService.GetTradesAsync();
            TradesDataGrid.ItemsSource = trades;
        }

        private void HistoryWindow_Activated(object? sender, System.EventArgs e)
        {
            // Al activarse la ventana, recargamos el historial para reflejar nuevas operaciones
            _ = CargarHistorialAsync();
        }

        public async System.Threading.Tasks.Task RefreshHistorialAsync()
        {
            await CargarHistorialAsync();
        }

        private async System.Threading.Tasks.Task CargarHistorialAsync()
        {
            var dbService = new DatabaseService();
            await dbService.InitializeAsync();
            var trades = await dbService.GetTradesAsync();
            TradesDataGrid.ItemsSource = trades;
        }
    }
}