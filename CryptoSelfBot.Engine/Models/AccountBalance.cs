namespace CryptoSelfBot.Engine.Models
{
    public class AccountBalance
    {
        public string Asset { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Available { get; set; }
        public decimal Locked { get; set; }
    }
}