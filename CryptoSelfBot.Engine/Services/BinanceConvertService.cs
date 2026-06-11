using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Clients;

namespace CryptoSelfBot.Engine.Services
{
    public class BinanceConvertService
    {
        private readonly Binance.Net.Clients.BinanceRestClient _client;

        public BinanceConvertService()
        {
            _client = new Binance.Net.Clients.BinanceRestClient();
        }
        public async Task<decimal?> EstimateConversionAsync(string fromSymbol, string toSymbol, decimal amount)
        {
            try
            {
                var priceRes = await _client.SpotApi.ExchangeData.GetPriceAsync(fromSymbol + toSymbol);
                if (priceRes.Success)
                {
                    return priceRes.Data.Price * amount;
                }

                // try reverse pair
                var rev = await _client.SpotApi.ExchangeData.GetPriceAsync(toSymbol + fromSymbol);
                if (rev.Success)
                {
                    return amount / rev.Data.Price;
                }
            }
            catch { }

            return null;
        }

        public async Task<bool> ExecuteConversionAsync(string fromSymbol, string toSymbol, decimal amount)
        {
            // Stub: en producción usar endpoint de conversion o mercado
            await Task.Delay(10);
            return false;
        }
    }
}
