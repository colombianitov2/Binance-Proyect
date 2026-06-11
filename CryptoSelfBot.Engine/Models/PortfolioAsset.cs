namespace CryptoSelfBot.Engine.Models
{
    public class PortfolioAsset
    {
        public string Asset { get; set; } =
            string.Empty;

        public decimal Free { get; set; }

        public decimal Locked { get; set; }

        public decimal Total =>
            Free + Locked;

        public decimal UsdtValue { get; set; }

        public bool IsFiat { get; set; }

        public bool IsStablecoin { get; set; }
    }
}