using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryptoSelfBot.Wpf.Models
{
    public class FiatAsset : INotifyPropertyChanged
    {
        private string _symbol = "";
        private string _value = "";

        public string Symbol
        {
            get => _symbol;
            set { _symbol = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
