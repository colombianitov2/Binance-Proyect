using System.Collections.Generic;

namespace CryptoSelfBot.Engine.Services
{
    public class MarketScanner
    {
        public List<string> GetDefaultPairs()
        {
            return new List<string>
            {
                "BTCUSDT",
                "ETHUSDT",
                "SOLUSDT",
                "BNBUSDT",
                "XRPUSDT",
                "DOGEUSDT",
                "ADAUSDT",
                "AVAXUSDT"
            };
        }
    }
}