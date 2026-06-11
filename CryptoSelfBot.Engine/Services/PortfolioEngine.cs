using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CryptoSelfBot.Engine.Models;

namespace CryptoSelfBot.Engine.Services
{
    public class PortfolioEngine
    {
        private readonly IBinanceService
            _binanceService;

        // =============================================
        // FIAT
        // =============================================

        private readonly HashSet<string>
            _fiatCurrencies =
                new()
                {
                    "COP",
                    "USD",
                    "EUR",
                    "GBP",
                    "JPY",
                    "BRL"
                };

        // =============================================
        // STABLES
        // =============================================

        private readonly HashSet<string>
            _stablecoins =
                new()
                {
                    "USDT",
                    "USDC",
                    "BUSD",
                    "FDUSD"
                };

        // =============================================
        // CONSTRUCTOR
        // =============================================

        public PortfolioEngine(
            IBinanceService binanceService)
        {
            _binanceService =
                binanceService;
        }

        // =============================================
        // SNAPSHOT
        // =============================================

        public async Task<PortfolioSnapshot>
            GetPortfolioSnapshotAsync()
        {
            var balances =
                await _binanceService
                    .GetBalancesAsync();

            var snapshot =
                new PortfolioSnapshot();

            foreach (var balance in balances)
            {
                var asset =
                    balance.Key;

                var quantity =
                    balance.Value;

                decimal usdtValue =
                    await EstimateUsdtValueAsync(
                        asset,
                        quantity);

                snapshot.Assets.Add(
                    new PortfolioAsset
                    {
                        Asset = asset,

                        Free = quantity,

                        Locked = 0,

                        UsdtValue = usdtValue,

                        IsFiat =
                            _fiatCurrencies
                                .Contains(asset),

                        IsStablecoin =
                            _stablecoins
                                .Contains(asset)
                    });
            }

            return snapshot;
        }

        // =============================================
        // ESTIMATE VALUE
        // =============================================

        private async Task<decimal>
            EstimateUsdtValueAsync(
                string asset,
                decimal quantity)
        {
            try
            {
                // =====================================
                // USDT DIRECT
                // =====================================

                if (asset == "USDT")
                {
                    return quantity;
                }

                // =====================================
                // FIAT MOCK
                // =====================================

                if (asset == "COP")
                {
                    return quantity / 3900m;
                }

                // =====================================
                // TRY MARKET PRICE
                // =====================================

                var symbol =
                    $"{asset}USDT";

                var price =
                    await _binanceService
                        .GetCurrentPriceAsync(
                            symbol);

                return price * quantity;
            }
            catch
            {
                return 0m;
            }
        }

        // =============================================
        // ANALYSIS
        // =============================================

        public string AnalyzePortfolio(
            PortfolioSnapshot snapshot)
        {
            if (snapshot.TotalUsdtValue <= 0)
            {
                return
                    "Sin capital disponible";
            }

            if (snapshot.StablecoinValue >
                snapshot.TotalUsdtValue * 0.8m)
            {
                return
                    "Alta liquidez disponible";
            }

            if (snapshot.CryptoValue >
                snapshot.TotalUsdtValue * 0.9m)
            {
                return
                    "Alta exposición crypto";
            }

            return
                "Portfolio balanceado";
        }
    }
}