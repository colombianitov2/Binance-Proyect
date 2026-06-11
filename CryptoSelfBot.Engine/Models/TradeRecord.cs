using System;

namespace CryptoSelfBot.Engine.Models
{
    public class TradeRecord
    {
        public int Id { get; set; }

        public string Symbol { get; set; } = string.Empty;

        public string Side { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal Quantity { get; set; }

        public DateTime TimestampUtc { get; set; }

        public string Strategy { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;
    }
}