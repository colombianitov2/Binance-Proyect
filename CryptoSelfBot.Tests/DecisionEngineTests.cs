using System.Threading.Tasks;
using CryptoSelfBot.Engine.Services;
using Xunit;

namespace CryptoSelfBot.Tests
{
    public class DecisionEngineTests
    {
        [Fact]
        public async Task DecisionEngine_ReturnsDecisionEnum()
        {
            var sentiment = new SentimentWeighter();
            var db = new CryptoSelfBot.Engine.Services.DatabaseService();
            var binance = new BinanceService();
            var backtest = new BacktestingService(db, binance);
            var engine = new DecisionEngine(sentiment, backtest);

            var decision = await engine.GetDecisionAsync("BTCUSDT");

            Assert.IsAssignableFrom<DecisionEngine.Decision>(decision);
        }
    }
}
