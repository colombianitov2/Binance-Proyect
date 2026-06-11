using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Clients;

namespace CryptoSelfBot.Engine.Services
{
    // Stub para interaccion con Binance Futures (requiere configuración y claves)
    public class BinanceFuturesService
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
                if (!ping.Success)
                {
                    LastError = ping.Error?.Message ?? "Ping failed";
                    IsConnected = false;
                    return false;
                }

                // Quick price check as connectivity test
                var price = await _client.SpotApi.ExchangeData.GetPriceAsync("BTCUSDT");
                // no-op change to force patch application
                if (!price.Success)
                {
                    LastError = price.Error?.Message ?? "Price check failed";
                    IsConnected = false;
                    return false;
                }

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

        public async Task<decimal> GetPriceAsync(string symbol)
        {
            try
            {
                if (_client == null)
                {
                    _client = new Binance.Net.Clients.BinanceRestClient();
                }
                var res = await _client.SpotApi.ExchangeData.GetPriceAsync(symbol);
                if (res.Success) return res.Data.Price;
            }
            catch { }
            return 0;
        }

        public async Task<bool> PlaceMarketOrderAsync(string symbol, decimal quantity, bool isBuy)
        {
            await Task.Delay(10);
            return false;
        }
    }
}
