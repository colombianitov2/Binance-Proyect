using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Clients;

namespace CryptoSelfBot.Engine.Services
{
    public class BinanceMarginService
    {
        private Binance.Net.Clients.BinanceRestClient? _client;
        public string LastError { get; private set; } = "";
        public bool IsConnected { get; private set; }

        public async Task<bool> ConnectAsync(string apiKey, string secret, bool testnet)
        {
            try
            {
                _client = new Binance.Net.Clients.BinanceRestClient(options => { options.Environment = testnet ? BinanceEnvironment.Testnet : BinanceEnvironment.Live; });
                var ping = await _client.SpotApi.ExchangeData.PingAsync();
                if (!ping.Success) { LastError = ping.Error?.Message ?? "Ping failed"; IsConnected = false; return false; }
                IsConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                IsConnected = false;
                return false;
            }
        }

        public async Task<bool> TransferAsync(string assetFrom, string assetTo, decimal amount)
        {
            await Task.Delay(10);
            return false;
        }
    }
}
