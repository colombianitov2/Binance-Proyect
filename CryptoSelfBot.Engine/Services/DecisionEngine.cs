using System;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public class DecisionEngine
    {
        private readonly SentimentWeighter _sentiment;
        private readonly BacktestingService _backtest;

        public DecisionEngine(SentimentWeighter sentiment, BacktestingService backtest)
        {
            _sentiment = sentiment;
            _backtest = backtest;
        }

        // Simple decision enum
        public enum Decision
        {
            Hold,
            Buy,
            Sell
        }

        public async Task<Decision> GetDecisionAsync(string symbol)
        {
            // Sentiment score stub
            var sentiment = await _sentiment.AnalyzeSentimentAsync(symbol);

            // TA stub: use backtest to evaluate historical edge (placeholder)
            var historicalEdge = await _backtest.EvaluateEdgeAsync(symbol);

            // Simple heuristic: combine signals
            if (sentiment > 0.6m && historicalEdge > 0.02m) return Decision.Buy;
            if (sentiment < -0.5m && historicalEdge < -0.01m) return Decision.Sell;
            return Decision.Hold;
        }
    }
}
