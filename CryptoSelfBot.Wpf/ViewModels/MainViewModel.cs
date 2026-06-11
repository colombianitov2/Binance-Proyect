using CryptoSelfBot.Wpf.Models;
using CryptoSelfBot.Engine.Services;
using CryptoSelfBot.Engine;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace CryptoSelfBot.Wpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CryptoAsset> CryptoAssets { get; set; }
        public ObservableCollection<FiatAsset> FiatAssets { get; set; }
        public ObservableCollection<string> LogsList { get; set; } = new();
        public ObservableCollection<string> Notifications { get; set; } = new();

        // Mantener propiedad Logs antigua para compatibilidad pero delegar en LogsList
        private string _logs = string.Empty;
        public string Logs
        {
            get => _logs;
            set { _logs = value; OnPropertyChanged(); }
        }

        private string _tradingStatus = "Parado";
        public string TradingStatus
        {
            get => _tradingStatus;
            set { _tradingStatus = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "Listo para operar";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _connectionStatus = "DESCONECTADO";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        private bool _isTradingActive;
        public bool IsTradingActive
        {
            get => _isTradingActive;
            set
            {
                _isTradingActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ToggleTradingText));
            }
        }

        public string ToggleTradingText => IsTradingActive ? "Detener Trading" : "Iniciar Trading";

        private string _currentDate = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        public string CurrentDate
        {
            get => _currentDate;
            set { _currentDate = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            CryptoAssets = new ObservableCollection<CryptoAsset>();
            FiatAssets = new ObservableCollection<FiatAsset>();
            var fiatList = new[] { "USD","EUR","COP","ARS","MXN","GBP","CAD","AUD","JPY","CHF","BRL","CLP","PEN","UYU","VEF","NZD","SGD","HKD","DKK","NOK" };
            foreach (var f in fiatList) FiatAssets.Add(new FiatAsset { Symbol = f, Value = "0" });
        }

        public void RefreshAllPrices()
        {
            StatusMessage = $"Precios actualizados a las {System.DateTime.Now:HH:mm:ss}";
        }

        public void UpsertCrypto(string symbol, string price, string change = "", bool isNegative = false)
        {
            var existing = CryptoAssets.FirstOrDefault(c => c.Symbol == symbol);
            if (existing == null)
            {
                CryptoAssets.Add(new CryptoAsset { Symbol = symbol, Price = price, Change = change, IsNegative = isNegative });
                return;
            }

            existing.Price = price;
            existing.Change = change;
            existing.IsNegative = isNegative;
        }

        public void UpsertFiat(string symbol, string value)
        {
            var existing = FiatAssets.FirstOrDefault(f => f.Symbol == symbol);
            if (existing == null)
            {
                FiatAssets.Add(new FiatAsset { Symbol = symbol, Value = value });
                return;
            }

            existing.Value = value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
