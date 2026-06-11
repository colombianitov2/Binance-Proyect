using System;

namespace CryptoSelfBot.Engine.Services
{
    public class DataImportanceClassifier
    {
        public double CalculateImportanceScore(MarketDataPoint data)
        {
            double score = 0.0;

            score += data.DataType switch
            {
                "price" => 10.0,
                "ohlcv" => 8.0,
                "volume" => 6.0,
                "sentiment" => 5.0,
                "news" => 3.0,
                _ => 1.0
            };

            score += data.Source switch
            {
                "binance" => 5.0,
                "coingecko" => 4.0,
                "coinmarketcap" => 4.0,
                "newsapi" => 2.0,
                _ => 1.0
            };

            if (data.OpenPrice.HasValue && data.ClosePrice.HasValue && data.OpenPrice.Value > 0)
            {
                double volatility = Math.Abs((double)(data.ClosePrice.Value - data.OpenPrice.Value) / (double)data.OpenPrice.Value);
                score += volatility * 20.0;
            }

            double daysAgo = (DateTime.UtcNow - data.Timestamp).TotalDays;
            if (daysAgo < 7) score += 5.0;
            else if (daysAgo < 30) score += 2.0;
            else if (daysAgo < 90) score += 0.5;

            return score;
        }
    }

    public class MarketDataPoint
    {
        public string Source { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public decimal? OpenPrice { get; set; }
        public decimal? ClosePrice { get; set; }
        public decimal? Volume { get; set; }
    }
}