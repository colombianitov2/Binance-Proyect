using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;
using CryptoSelfBot.Engine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public class BinanceSpotService : IBinanceService
    {
        private readonly BinanceRestClient _client;
        public bool IsRunning { get; private set; }
        public TradingEnvironment Environment { get; }

        public BinanceSpotService(
            string apiKey,
            string apiSecret,
            TradingEnvironment environment = TradingEnvironment.Testnet)
        {
            Environment = environment;
            _client = new BinanceRestClient(options =>
            {
                options.Environment = environment == TradingEnvironment.Testnet
                    ? BinanceEnvironment.Testnet
                    : BinanceEnvironment.Live;
                options.ApiCredentials = new BinanceCredentials(apiKey, apiSecret);
            });
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var result = await _client.SpotApi.ExchangeData.PingAsync();
                return result.Success;
            }
            catch { return false; }
        }

        public async Task<bool> ValidateCredentialsAsync()
        {
            try
            {
                var result = await _client.SpotApi.Account.GetAccountInfoAsync();
                return result.Success;
            }
            catch { return false; }
        }

        public async Task<Dictionary<string, decimal>> GetBalancesAsync()
        {
            var result = await _client.SpotApi.Account.GetAccountInfoAsync();
            if (!result.Success) throw new Exception(result.Error?.Message);
            return result.Data.Balances.Where(b => b.Total > 0).ToDictionary(b => b.Asset, b => b.Total);
        }

        public async Task<decimal> GetCurrentPriceAsync(string symbol)
        {
            var result = await _client.SpotApi.ExchangeData.GetPriceAsync(symbol);
            if (!result.Success) throw new Exception(result.Error?.Message);
            return result.Data.Price;
        }

        public async Task<bool> PlaceMarketOrderAsync(string symbol, decimal quantity, bool isBuy)
        {
            var side = isBuy ? OrderSide.Buy : OrderSide.Sell;
            var result = await _client.SpotApi.Trading.PlaceOrderAsync(symbol, side, SpotOrderType.Market, quantity: quantity);
            if (!result.Success) throw new Exception(result.Error?.Message);
            return true;
        }

        public async Task<string> GetAccountStatusAsync()
        {
            try
            {
                var result = await _client.SpotApi.Account.GetAccountInfoAsync();
                return result.Success ? "CONECTADO" : "ERROR";
            }
            catch { return "DESCONECTADO"; }
        }

        public Task StartTradingAsync()
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopTradingAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }
    }
}