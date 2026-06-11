namespace CryptoSelfBot.Engine.Services
{
    public static class EngineServiceFactory
    {
        public static DecisionEngine CreateDecisionEngine()
        {
            var sentiment = new SentimentWeighter();
            var db = new DatabaseService();
            var binance = new BinanceService();
            var backtest = new BacktestingService(db, binance);
            return new DecisionEngine(sentiment, backtest);
        }
    }
}
