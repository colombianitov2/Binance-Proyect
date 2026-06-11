using CryptoSelfBot.Engine.Services;
using System.Linq;
using System.Windows;

namespace CryptoSelfBot.Wpf.Views
{
    public partial class NewsWindow : Window
    {
        private readonly DatabaseService _databaseService;

        public NewsWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            CargarNoticias();
        }

        private async void CargarNoticias()
        {
            await _databaseService.InitializeAsync();
            var trades = await _databaseService.GetTradesAsync();
            var newsTrades = trades
                .Where(t => t.Symbol == "SENTIMENT" || t.Notes?.Contains("NewsAPI") == true)
                .OrderByDescending(t => t.TimestampUtc)
                .ToList();

            NewsDataGrid.ItemsSource = newsTrades;

            var lastSentiment = newsTrades.FirstOrDefault();
            if (lastSentiment != null)
            {
                SentimentSummaryText.Text = $"Último sentimiento: {lastSentiment.Side} (Score: {lastSentiment.Price:F4}) - {lastSentiment.TimestampUtc:dd/MM/yyyy HH:mm}";
            }
            else
            {
                SentimentSummaryText.Text = "No hay datos de sentimiento disponibles.";
            }
        }

        private async void ExportNews_Click(object sender, RoutedEventArgs e)
        {
            var archiveService = new NewsArchiveService(
                _databaseService,
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CryptoSelfBot"));

            await archiveService.ExportNewsToExcelAsync();
            MessageBox.Show("Noticias exportadas correctamente.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}