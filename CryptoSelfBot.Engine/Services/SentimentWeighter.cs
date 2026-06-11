using System;
using System.Collections.Generic;
using System.Linq;

namespace CryptoSelfBot.Engine.Services
{
    public class SentimentWeighter
    {
        private readonly Dictionary<string, SourceStats> _sourceStats = new();

        public void RecordDecision(string source, bool wasCorrect)
        {
            if (!_sourceStats.ContainsKey(source))
                _sourceStats[source] = new SourceStats();

            var stats = _sourceStats[source];
            stats.TotalDecisions++;
            if (wasCorrect)
                stats.CorrectDecisions++;
        }

        public decimal GetWeight(string source)
        {
            if (!_sourceStats.ContainsKey(source) || _sourceStats[source].TotalDecisions < 3)
                return 1.0m; // Peso neutro mientras no haya suficiente historial

            var stats = _sourceStats[source];
            decimal accuracy = (decimal)stats.CorrectDecisions / stats.TotalDecisions;
            // Escala el peso entre 0.5 (muy mala) y 2.0 (muy buena)
            return 0.5m + accuracy * 1.5m;
        }

        public Dictionary<string, decimal> GetAllWeights()
        {
            return _sourceStats.ToDictionary(kvp => kvp.Key, kvp => GetWeight(kvp.Key));
        }

        public async Task<decimal> AnalyzeSentimentAsync(string symbol)
        {
            // Stub: devuelve valor entre -1 y 1
            await Task.Delay(10);
            var rnd = new Random(symbol.GetHashCode() ^ DateTime.Now.Millisecond);
            return (decimal)(rnd.NextDouble() * 2.0 - 1.0);
        }

        private class SourceStats
        {
            public int TotalDecisions { get; set; }
            public int CorrectDecisions { get; set; }
        }
    }
}