namespace CryptoSelfBot.Engine.Models
{
    public class RiskSettings
    {
        public decimal MaxTradeAmountUsdt { get; set; } = 50m;

        public bool AutoMaxTradeAmount { get; set; } = true;

        public int MaxOpenPositions { get; set; } = 3;

        public decimal MaxDailyLossPercent { get; set; } = 5m;

        public int CooldownSeconds { get; set; } = 30;

        public bool EnableTrading { get; set; } = true;
    }
}
