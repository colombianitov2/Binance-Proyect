using Binance.Net.Objects.Models.Spot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public class TradingEngine
    {
        private readonly BinanceService _binanceService;
        private readonly RiskManager _riskManager;
        private readonly DecisionEngine _decisionEngine;
        public bool PaperTrading { get; set; } = false;

        private CancellationTokenSource? _token;

        public bool IsRunning { get; private set; }

        public string Status { get; private set; } =
            "Detenido";

        public event Action<string>? OnLog;

        public event Action<string>? OnStatusChanged;

        public TradingEngine(
            BinanceService binanceService,
            DecisionEngine decisionEngine)
        {
            _binanceService = binanceService;
            _riskManager = new RiskManager();
            _decisionEngine = decisionEngine;
        }

        // Convenience constructor to create default dependencies
        public TradingEngine(BinanceService binanceService) : this(binanceService, EngineServiceFactory.CreateDecisionEngine()) { }

        public async Task<bool> StartAsync()
        {
            try
            {
                if (IsRunning)
                    return true;

                if (!_binanceService.IsConnected)
                {
                    Log("Binance no conectado.");
                    return false;
                }

                _token =
                    new CancellationTokenSource();

                IsRunning = true;

                Status = "Trading iniciado";

                OnStatusChanged?.Invoke(Status);

                Log("Motor de trading iniciado.");

                _ = Task.Run(async () =>
                {
                    while (!_token.IsCancellationRequested)
                    {
                        await ExecuteCycleAsync();

                        await Task.Delay(
                            5000,
                            _token.Token);
                    }

                }, _token.Token);

                return true;
            }
            catch (Exception ex)
            {
                Log(ex.Message);

                return false;
            }
        }

        public void Stop()
        {
            try
            {
                _token?.Cancel();

                IsRunning = false;

                Status = "Trading detenido";

                OnStatusChanged?.Invoke(Status);

                Log("Motor detenido.");
            }
            catch
            {
            }
        }

        private async Task ExecuteCycleAsync()
        {
            try
            {
                Log("Escaneando mercado...");

                decimal btc =
                    await _binanceService
                        .GetPriceAsync("BTCUSDT");

                decimal eth =
                    await _binanceService
                        .GetPriceAsync("ETHUSDT");

                decimal sol =
                    await _binanceService
                        .GetPriceAsync("SOLUSDT");

                Log($"BTCUSDT = {btc}");
                Log($"ETHUSDT = {eth}");
                Log($"SOLUSDT = {sol}");

                if (btc > 0)
                {
                    Log("Mercado operativo.");

                    // Obtener decisión combinada del DecisionEngine para BTC
                    var btcDecision = await _decisionEngine.GetDecisionAsync("BTCUSDT");
                    Log("DecisionEngine(BTCUSDT): " + btcDecision.ToString());

                    // Ejemplo de decisión de trading simplificada respetando RiskManager
                    decimal tradeAmount = 10m; // placeholder
                    decimal totalCapital = 1000m; // placeholder
                    if (_riskManager.CanOpenTrade(tradeAmount, totalCapital))
                    {
                        if (!PaperTrading)
                        {
                            // Real trading (placeholder)
                        }
                        else
                        {
                            Log("PaperTrading: Simulando orden de compra");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        }

        public async Task<List<BinanceBalance>> GetBalancesAsync()
        {
            return await _binanceService
                .GetBalancesAsync();
        }

        private void Log(string message)
        {
            OnLog?.Invoke(
                $"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}