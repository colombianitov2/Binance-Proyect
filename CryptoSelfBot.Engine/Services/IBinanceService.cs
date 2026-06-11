using CryptoSelfBot.Engine.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public interface IBinanceService
    {
        bool IsRunning { get; }
        TradingEnvironment Environment { get; }
        Task<bool> TestConnectionAsync();
        Task<bool> ValidateCredentialsAsync();
        Task<Dictionary<string, decimal>> GetBalancesAsync();
        Task<decimal> GetCurrentPriceAsync(string symbol);
        Task<bool> PlaceMarketOrderAsync(string symbol, decimal quantity, bool isBuy);
        Task<string> GetAccountStatusAsync();
        Task StartTradingAsync();
        Task StopTradingAsync();
    }
}