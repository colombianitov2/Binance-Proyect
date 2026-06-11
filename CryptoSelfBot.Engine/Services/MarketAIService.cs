using Flurl;
using Flurl.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public class MarketAIService
    {
        private const string NewsApiKey = "da18f6e1f1fb4544880a1ebd5cd54590";
        private const string NewsApiUrl = "https://newsapi.org/v2/everything";
        private const string PythonNlpUrl = "http://127.0.0.1:5000/api/nlp/sentiment";

        private readonly DatabaseService _databaseService;

        private static readonly HashSet<string> BullishWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "bull", "surge", "rally", "gain", "soar", "upward", "growth", "record", "adoption",
            "partnership", "launch", "breakthrough", "approval", "etf", "accumulation",
            "bullish", "long", "support", "recovery", "momentum", "positive", "green"
        };

        private static readonly HashSet<string> BearishWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "bear", "crash", "drop", "fall", "decline", "sell-off", "loss", "hack",
            "exploit", "ban", "regulation", "crackdown", "fine", "lawsuit", "suspend",
            "bearish", "short", "resistance", "fear", "uncertainty", "doubt", "negative"
        };

        public MarketAIService(DatabaseService? databaseService = null)
        {
            _databaseService = databaseService ?? new DatabaseService();
        }

        public async Task<MarketSentimentResult> GetMarketSentimentAsync()
        {
            try
            {
                var response = await NewsApiUrl
                    .SetQueryParams(new
                    {
                        q = "cryptocurrency OR bitcoin OR crypto",
                        sortBy = "publishedAt",
                        pageSize = 20,
                        apiKey = NewsApiKey
                    })
                    .WithHeader("User-Agent", "CryptoSelfBot/1.0")
                    .GetJsonAsync<NewsApiResponse>();

                if (response?.Articles == null || !response.Articles.Any())
                    return NeutralResult("No se encontraron noticias.");

                decimal averageSentiment;
                try
                {
                    var headlines = response.Articles.Select(a => a.Title).Take(10).ToList();
                    var nlpResult = await PythonNlpUrl
                        .WithTimeout(3)
                        .PostJsonAsync(headlines)
                        .ReceiveJson<NlpSentimentResponse>();
                    averageSentiment = nlpResult?.Score ?? 0;
                }
                catch
                {
                    var sentiments = new List<decimal>();
                    foreach (var article in response.Articles)
                    {
                        decimal sentiment = AnalyzeSentiment(article.Title + " " + (article.Description ?? ""));
                        sentiments.Add(sentiment);
                    }
                    averageSentiment = sentiments.Average();
                }

                string outlook = averageSentiment switch
                {
                    > 0.25m => "Bullish",
                    < -0.25m => "Bearish",
                    _ => "Neutral"
                };

                // Registrar el sentimiento en la base de datos
                await _databaseService.InsertTradeAsync(new Models.TradeRecord
                {
                    Symbol = "SENTIMENT",
                    Side = outlook.ToUpper(),
                    Price = averageSentiment,
                    Quantity = 0,
                    TimestampUtc = DateTime.UtcNow,
                    Strategy = "NewsAPI",
                    Notes = $"Sentimiento: {outlook} (Score: {averageSentiment:F2})"
                });

                return new MarketSentimentResult
                {
                    OverallSentiment = outlook,
                    ShortTermOutlook = outlook,
                    MidTermOutlook = outlook,
                    LongTermOutlook = outlook,
                    SentimentScore = Math.Round(averageSentiment, 2),
                    KeyFactors = response.Articles.Select(a => a.Title).ToList(),
                    SourceWeight = 1.0m
                };
            }
            catch (Exception ex)
            {
                return new MarketSentimentResult
                {
                    OverallSentiment = "Neutral",
                    SentimentScore = 0m,
                    KeyFactors = new List<string> { "Error al obtener noticias: " + ex.Message },
                    SourceWeight = 0m
                };
            }
        }

        private decimal AnalyzeSentiment(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0m;
            var words = text.Split(' ', ',', '.', ';', ':', '!', '?');
            int bullishCount = words.Count(w => BullishWords.Contains(w));
            int bearishCount = words.Count(w => BearishWords.Contains(w));
            if (bullishCount + bearishCount == 0) return 0m;
            return (decimal)(bullishCount - bearishCount) / (bullishCount + bearishCount);
        }

        private MarketSentimentResult NeutralResult(string reason) =>
            new MarketSentimentResult
            {
                OverallSentiment = "Neutral",
                KeyFactors = new List<string> { reason }
            };
    }

    internal class NewsApiResponse
    {
        public List<NewsArticle> Articles { get; set; } = new();
    }

    internal class NewsArticle
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    internal class NlpSentimentResponse
    {
        public decimal Score { get; set; }
    }

    public class MarketSentimentResult
    {
        public string OverallSentiment { get; set; } = "Neutral";
        public string ShortTermOutlook { get; set; } = "Neutral";
        public string MidTermOutlook { get; set; } = "Neutral";
        public string LongTermOutlook { get; set; } = "Neutral";
        public decimal SentimentScore { get; set; }
        public List<string> KeyFactors { get; set; } = new();
        public decimal SourceWeight { get; set; } = 1.0m;
    }
}