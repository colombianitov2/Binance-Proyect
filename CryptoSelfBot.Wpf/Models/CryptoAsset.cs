using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryptoSelfBot.Wpf.Models
{
    public class CryptoAsset : INotifyPropertyChanged
    {
        private string _symbol = "";
        private string _price = "";
        private string _change = "";
        private bool _isNegative;

        public string Symbol
        {
            get => _symbol;
            set { _symbol = value; OnPropertyChanged(); }
        }

        public string Price
        {
            get => _price;
            set { _price = value; OnPropertyChanged(); }
        }

        public string Change
        {
            get => _change;
            set { _change = value; OnPropertyChanged(); }
        }

        public bool IsNegative
        {
            get => _isNegative;
            set { _isNegative = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
