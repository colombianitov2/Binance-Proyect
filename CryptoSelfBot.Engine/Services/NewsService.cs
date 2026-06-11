using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public class NewsService : IDisposable
    {
        private readonly DatabaseService _database;
        private readonly NewsArchiveService _archiveService;
        private readonly SentimentWeighter _weigher = new();
        private Timer? _dailyTimer;
        private readonly string _storagePath;

        public NewsService(DatabaseService database, string storagePath)
        {
            _database = database;
            _storagePath = storagePath;
            _archiveService = new NewsArchiveService(database, storagePath);
        }

        public void StartBackgroundTasks()
        {
            // Programar ejecución diaria para limpieza y sincronización (periodo 24h)
            _dailyTimer = new Timer(async _ => await DailyTickAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(24));
        }

        private async Task DailyTickAsync()
        {
            try
            {
                // Ejecutar limpieza de noticias antiguas (retención 365 días)
                await _archiveService.CleanupOldNewsAsync(365);

                // Intentar obtener noticias nuevas (stub)
                await FetchLatestAndStoreAsync();
            }
            catch { /* no romper el timer */ }
        }

        public async Task FetchLatestAndStoreAsync(string? newsApiKey = null)
        {
            // Si no hay API Key, generar entradas mock para UI y pruebas
            List<(string title, string content, string source, DateTime when)> articles = new();

            if (string.IsNullOrEmpty(newsApiKey))
            {
                // Mock: 2 noticias
                articles.Add(("Mercado alcista en BTC", "Se espera recuperación tras consolidación.", "MockNews", DateTime.UtcNow));
                articles.Add(("Noticias macro negativas", "Datos de inflación muestran presiones; riesgo en corto plazo.", "MockNews", DateTime.UtcNow));
            }
            else
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    string url = $"https://newsapi.org/v2/top-headlines?language=es&pageSize=5&apiKey={newsApiKey}";
                    var res = await client.GetStringAsync(url);
                    using var doc = JsonDocument.Parse(res);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("articles", out var arr))
                    {
                        foreach (var it in arr.EnumerateArray())
                        {
                            string title = it.GetProperty("title").GetString() ?? "";
                            string content = it.GetProperty("description").GetString() ?? "";
                            string source = it.GetProperty("source").GetProperty("name").GetString() ?? "NewsAPI";
                            DateTime when = DateTime.UtcNow;
                            articles.Add((title, content, source, when));
                        }
                    }
                }
                catch
                {
                    // fallback a mock si falla
                    articles.Add(("NewsAPI error","No se pudieron obtener noticias","NewsAPI",DateTime.UtcNow));
                }
            }

            // Analizar y almacenar
            foreach (var a in articles)
            {
                decimal score = AnalyzeSentimentScore(a.title + " " + a.content);
                string decision = score > 0.05m ? "Bullish" : (score < -0.05m ? "Bearish" : "Neutral");

                var record = new CryptoSelfBot.Engine.Models.TradeRecord
                {
                    Symbol = "SENTIMENT",
                    Side = decision,
                    Price = (decimal)score,
                    Quantity = 0,
                    TimestampUtc = a.when,
                    Strategy = "NewsSentiment",
                    Notes = $"Source:{a.source};Title:{a.title}"
                };

                await _database.InsertTradeAsync(record);
            }
        }

        private decimal AnalyzeSentimentScore(string text)
        {
            // Heurística muy simple: palabras positivas/negativas
            string lower = text.ToLowerInvariant();
            int positive = 0, negative = 0;
            string[] posWords = new[] { "alcista", "recuper", "sube", "ganador", "positivo" };
            string[] negWords = new[] { "inflación", "negativo", "baja", "caída", "riesgo" };
            foreach (var p in posWords) if (lower.Contains(p)) positive++;
            foreach (var n in negWords) if (lower.Contains(n)) negative++;

            int total = positive + negative;
            if (total == 0) return 0;
            return (decimal)(positive - negative) / total; // -1..1
        }

        public void Dispose()
        {
            try { _dailyTimer?.Dispose(); } catch { }
        }
    }
}
