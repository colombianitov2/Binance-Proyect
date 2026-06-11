using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CryptoSelfBot.Engine.Models;

namespace CryptoSelfBot.Engine.Services
{
    public class BacktestingService
    {
        private readonly DatabaseService _databaseService;
        private readonly BinanceService _binanceService;

        public BacktestingService(DatabaseService databaseService, BinanceService binanceService)
        {
            _databaseService = databaseService;
            _binanceService = binanceService;
        }

        public async Task<BacktestResult> RunBacktestAsync(string symbol, DateTime from, DateTime to, decimal initialCapital = 100m)
        {
            // Obtener operaciones históricas almacenadas en SQLite
            var trades = await _databaseService.GetTradesAsync();
            var filteredTrades = trades
                .Where(t => t.Symbol == symbol || t.Symbol.StartsWith(symbol.Replace("USDT", "")))
                .Where(t => t.TimestampUtc >= from && t.TimestampUtc <= to)
                .OrderBy(t => t.TimestampUtc)
                .ToList();

            var result = new BacktestResult
            {
                Symbol = symbol,
                InitialCapital = initialCapital,
                StartDate = from,
                EndDate = to
            };

            if (!filteredTrades.Any())
            {
                result.Summary = "Sin datos históricos para el período seleccionado.";
                return result;
            }

            decimal capital = initialCapital;
            decimal cryptoHolding = 0;
            decimal peakCapital = initialCapital;
            decimal maxDrawdown = 0;

            foreach (var trade in filteredTrades)
            {
                if (trade.Side == "Buy")
                {
                    decimal cost = trade.Price * trade.Quantity;
                    if (capital >= cost)
                    {
                        capital -= cost;
                        cryptoHolding += trade.Quantity;
                    }
                }
                else if (trade.Side == "Sell")
                {
                    if (cryptoHolding >= trade.Quantity)
                    {
                        decimal revenue = trade.Price * trade.Quantity;
                        capital += revenue;
                        cryptoHolding -= trade.Quantity;
                    }
                }

                decimal totalValue = capital + cryptoHolding * trade.Price;
                if (totalValue > peakCapital)
                    peakCapital = totalValue;
                decimal drawdown = (peakCapital - totalValue) / peakCapital;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            result.FinalCapital = capital;
            result.CryptoHolding = cryptoHolding;
            result.ProfitLoss = capital - initialCapital;
            result.ProfitLossPercentage = (capital - initialCapital) / initialCapital * 100m;
            result.MaxDrawdown = maxDrawdown;
            result.PeakCapital = peakCapital;
            result.TotalTrades = filteredTrades.Count;
            result.WinningTrades = filteredTrades.Count(t => t.Side == "Sell" && t.Price > 0);
            result.LosingTrades = result.TotalTrades - result.WinningTrades;

            result.Summary = $"Backtesting completado: {result.TotalTrades} operaciones, " +
                             $"P&L: {result.ProfitLoss:F2} USDT ({result.ProfitLossPercentage:F1}%), " +
                             $"Drawdown máximo: {result.MaxDrawdown:P1}";

            return result;
        }
        public async Task<decimal> EvaluateEdgeAsync(string symbol)
        {
            // Stub: returns a small random edge for demo purposes
            await Task.Delay(5);
            var rnd = new Random(symbol.GetHashCode() ^ DateTime.Now.Millisecond);
            return (decimal)(rnd.NextDouble() * 0.04 - 0.02); // -0.02 .. 0.02
        }
    }

    public class BacktestResult
    {
        public string Symbol { get; set; } = "";
        public decimal InitialCapital { get; set; }
        public decimal FinalCapital { get; set; }
        public decimal CryptoHolding { get; set; }
        public decimal ProfitLoss { get; set; }
        public decimal ProfitLossPercentage { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal PeakCapital { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Summary { get; set; } = "";
    }
}