using System.Collections.Generic;
using System.Linq;

namespace CryptoSelfBot.Engine.Models
{
    public class PortfolioSnapshot
    {
        public List<PortfolioAsset>
            Assets
        { get; set; } =
                new();

        public decimal TotalUsdtValue =>
            Assets.Sum(x => x.UsdtValue);

        public decimal FiatValue =>
            Assets
                .Where(x => x.IsFiat)
                .Sum(x => x.UsdtValue);

        public decimal StablecoinValue =>
            Assets
                .Where(x => x.IsStablecoin)
                .Sum(x => x.UsdtValue);

        public decimal CryptoValue =>
            Assets
                .Where(x =>
                    !x.IsFiat &&
                    !x.IsStablecoin)
                .Sum(x => x.UsdtValue);
    }
}