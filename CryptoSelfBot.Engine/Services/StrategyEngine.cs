using System.Threading.Tasks;
using CryptoSelfBot.Engine.Models;

namespace CryptoSelfBot.Engine.Services
{
    public class StrategyEngine
    {
        // =====================================================
        // ANÁLISIS PRINCIPAL
        // =====================================================

        public async Task<TradingSignal> AnalyzeMarketAsync(
            string symbol,
            decimal currentPrice)
        {
            // Simulación async moderna
            await Task.Delay(1);

            // =================================================
            // REGLA SIMPLE TEMPORAL
            // =================================================

            // TODO:
            // Aquí luego entrará:
            // - IA
            // - sentimiento
            // - noticias
            // - WebSocket
            // - riesgo
            // - volumen
            // - RSI
            // - MACD
            // - order book
            // - volatilidad

            if (currentPrice < 95000m)
            {
                return TradingSignal.Buy;
            }

            if (currentPrice > 120000m)
            {
                return TradingSignal.Sell;
            }

            return TradingSignal.Hold;
        }
    }
}