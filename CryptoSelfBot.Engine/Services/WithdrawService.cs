using System;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public class WithdrawService
    {
        private readonly BinanceService _binance;

        public WithdrawService(BinanceService binance)
        {
            _binance = binance;
        }

        public async Task<(bool success, string message)> ConsolidateToAsync(string targetAsset)
        {
            if (!_binance.IsConnected) return (false, "Binance no conectado");

            var balances = await _binance.GetBalancesAsync();
            bool any = false;

            foreach (var b in balances)
            {
                if (b.Asset == targetAsset) continue;
                if (b.Total <= 0) continue;

                string symbol = b.Asset + targetAsset;
                var price = await _binance.GetPriceAsync(symbol);
                if (price > 0)
                {
                    bool ok = await _binance.PlaceMarketOrderAsync(symbol, b.Available, false);
                    if (ok) any = true;
                }
            }

            return any ? (true, "Consolidación realizada") : (false, "No se realizaron operaciones");
        }
    }
}
