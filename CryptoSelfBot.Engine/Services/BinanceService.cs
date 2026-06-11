using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CryptoSelfBot.Engine.Models;

namespace CryptoSelfBot.Engine.Services
{
    public class BinanceService : IBinanceService
    {
        private BinanceRestClient? _client;
        public bool IsConnected { get; private set; }
        public bool IsRunning { get; private set; }
        public TradingEnvironment Environment { get => _environment == BinanceEnvironment.Testnet ? TradingEnvironment.Testnet : TradingEnvironment.Live; }
        public string LastError { get; private set; } = "";

        private readonly int _maxRetries = 3;
        private readonly TimeSpan _baseDelay = TimeSpan.FromSeconds(1);
        private BinanceEnvironment _environment = BinanceEnvironment.Live;

        public async Task<bool> ConnectAsync(string apiKey, string secretKey, bool testnet)
        {
            try
            {
                _environment = testnet ? BinanceEnvironment.Testnet : BinanceEnvironment.Live;
                _client = new BinanceRestClient(options =>
                {
                    options.Environment = _environment;

                    if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(secretKey))
                    {
                        options.ApiCredentials = new BinanceCredentials(apiKey, secretKey);
                    }
                });

                // Verificar conectividad y credenciales con reintentos simples
                int attempt = 0;
                while (attempt < _maxRetries)
                {
                    attempt++;
                    try
                    {
                        var pingResult = await _client.SpotApi.ExchangeData.PingAsync();
                        if (!pingResult.Success)
                        {
                            LastError = pingResult.Error?.Message ?? "No se pudo contactar con Binance";
                            await Task.Delay(_baseDelay * attempt);
                            continue;
                        }

                        var accountInfo = await _client.SpotApi.Account.GetAccountInfoAsync();
                        if (!accountInfo.Success)
                        {
                            LastError = accountInfo.Error?.Message ?? "Error al validar credenciales";
                            await Task.Delay(_baseDelay * attempt);
                            continue;
                        }

                        IsConnected = true;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LastError = ex.Message;
                        await Task.Delay(_baseDelay * attempt);
                    }
                }

                IsConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                IsConnected = false;
                return false;
            }
        }

        // Original low-level balances retrieval
        public async Task<System.Collections.Generic.List<BinanceBalance>> GetBalancesAsync()
        {
            if (_client == null || !IsConnected)
                return new List<BinanceBalance>();

            var result = await _client.SpotApi.Account.GetAccountInfoAsync();
            if (!result.Success)
                return new List<BinanceBalance>();

            return result.Data.Balances
                .Where(b => b.Total > 0)
                .ToList();
        }

        // IBinanceService compatible wrapper
        public async Task<System.Collections.Generic.Dictionary<string, decimal>> GetBalancesAsyncWrapper()
        {
            var list = await GetBalancesAsync();
            var dict = new Dictionary<string, decimal>();
            foreach (var b in list)
            {
                dict[b.Asset] = b.Total;
            }
            return dict;
        }

        public async Task<decimal> GetPriceAsync(string symbol)
        {
            if (_client == null || !IsConnected)
                return 0;

            int attempt = 0;
            while (attempt < _maxRetries)
            {
                attempt++;
                try
                {
                    var ticker = await _client.SpotApi.ExchangeData.GetPriceAsync(symbol);
                    if (ticker.Success)
                        return ticker.Data.Price;

                    LastError = ticker.Error?.Message ?? "Error obteniendo precio";
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                }

                await Task.Delay(_baseDelay * attempt);
            }

            return 0;
        }

        public async Task<bool> PlaceMarketOrderAsync(string symbol, decimal quantity, bool isBuy)
        {
            if (_client == null || !IsConnected)
                return false;

            var side = isBuy
                ? Binance.Net.Enums.OrderSide.Buy
                : Binance.Net.Enums.OrderSide.Sell;

            int attempt = 0;
            while (attempt < _maxRetries)
            {
                attempt++;
                try
                {
                    var order = await _client.SpotApi.Trading.PlaceOrderAsync(
                        symbol,
                        side,
                        Binance.Net.Enums.SpotOrderType.Market,
                        quantity: quantity);

                    if (order.Success)
                        return true;

                    LastError = order.Error?.Message ?? "Error al colocar orden";
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                }

                await Task.Delay(_baseDelay * attempt);
            }

            return false;
        }

        public void UpdateCredentials(string apiKey, string secretKey)
        {
            // Recrear cliente con nuevas credenciales manteniendo el entorno previo
            var env = _environment;
            _client?.Dispose();
            _client = new BinanceRestClient(options =>
            {
                options.Environment = env;
                if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(secretKey))
                {
                    options.ApiCredentials = new BinanceCredentials(apiKey, secretKey);
                }
            });
        }

        public void Disconnect()
        {
            try { _client?.Dispose(); } catch { }
            IsConnected = false;
        }

        // Explicit interface implementations for IBinanceService
        async Task<bool> IBinanceService.TestConnectionAsync()
        {
            if (_client == null) return false;
            try
            {
                var ping = await _client.SpotApi.ExchangeData.PingAsync();
                return ping.Success;
            }
            catch { return false; }
        }

        async Task<bool> IBinanceService.ValidateCredentialsAsync()
        {
            if (_client == null) return false;
            try
            {
                var info = await _client.SpotApi.Account.GetAccountInfoAsync();
                return info.Success;
            }
            catch { return false; }
        }

        async Task<System.Collections.Generic.Dictionary<string, decimal>> IBinanceService.GetBalancesAsync()
        {
            return await GetBalancesAsyncWrapper();
        }

        async Task<decimal> IBinanceService.GetCurrentPriceAsync(string symbol)
        {
            return await GetPriceAsync(symbol);
        }

        async Task<bool> IBinanceService.PlaceMarketOrderAsync(string symbol, decimal quantity, bool isBuy)
        {
            return await PlaceMarketOrderAsync(symbol, quantity, isBuy);
        }

        async Task<string> IBinanceService.GetAccountStatusAsync()
        {
            if (_client == null || !IsConnected) return "Disconnected";
            try
            {
                var info = await _client.SpotApi.Account.GetAccountInfoAsync();
                if (!info.Success) return "Error: " + (info.Error?.Message ?? "Unknown");
                return $"Balances: {info.Data.Balances.Count(b => b.Total > 0)} assets";
            }
            catch (Exception ex) { return "Error: " + ex.Message; }
        }

        async Task IBinanceService.StartTradingAsync()
        {
            IsRunning = true;
            await Task.CompletedTask;
        }

        async Task IBinanceService.StopTradingAsync()
        {
            IsRunning = false;
            await Task.CompletedTask;
        }
    }
}
