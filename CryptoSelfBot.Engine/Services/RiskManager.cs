using System;
using CryptoSelfBot.Engine.Models;

namespace CryptoSelfBot.Engine.Services
{
    public class RiskManager
    {
        private readonly RiskSettings _settings;
        private RiskSettings _mutableSettings;
        private int _openPositions;
        private decimal _dailyLoss;
        private DateTime _lastTradeTime = DateTime.MinValue;
        private DateTime _dayStart = DateTime.UtcNow.Date;

        public RiskManager(RiskSettings? settings = null)
        {
            _settings = settings ?? new RiskSettings();
            _mutableSettings = new RiskSettings
            {
                MaxTradeAmountUsdt = _settings.MaxTradeAmountUsdt,
                AutoMaxTradeAmount = _settings.AutoMaxTradeAmount,
                MaxOpenPositions = _settings.MaxOpenPositions,
                MaxDailyLossPercent = _settings.MaxDailyLossPercent,
                CooldownSeconds = _settings.CooldownSeconds,
                EnableTrading = _settings.EnableTrading
            };
        }

        public void UpdateSettings(RiskSettings newSettings)
        {
            if (newSettings == null) return;
            _mutableSettings.MaxTradeAmountUsdt = newSettings.MaxTradeAmountUsdt;
            _mutableSettings.AutoMaxTradeAmount = newSettings.AutoMaxTradeAmount;
            _mutableSettings.MaxOpenPositions = newSettings.MaxOpenPositions;
            _mutableSettings.MaxDailyLossPercent = newSettings.MaxDailyLossPercent;
            _mutableSettings.CooldownSeconds = newSettings.CooldownSeconds;
            _mutableSettings.EnableTrading = newSettings.EnableTrading;
        }

        public bool CanOpenTrade(decimal tradeAmountUsdt, decimal totalCapital)
        {
            if (!_mutableSettings.EnableTrading)
                return false;

            if (_openPositions >= _mutableSettings.MaxOpenPositions)
                return false;

            if (!_mutableSettings.AutoMaxTradeAmount && tradeAmountUsdt > _mutableSettings.MaxTradeAmountUsdt)
                return false;

            if (totalCapital <= 0)
                return false;

            decimal percentage = (tradeAmountUsdt / totalCapital) * 100m;
            if (percentage > 5m) // máximo 5% del capital por operación (podría parametrizarse)
                return false;

            if ((DateTime.UtcNow - _lastTradeTime).TotalSeconds < _mutableSettings.CooldownSeconds)
                return false;

            // Reiniciar pérdida diaria si cambió el día
            if (DateTime.UtcNow.Date != _dayStart.Date)
            {
                _dayStart = DateTime.UtcNow.Date;
                _dailyLoss = 0;
            }

            // Verificar límite de pérdida diaria
            if (_dailyLoss < -_mutableSettings.MaxDailyLossPercent / 100m * totalCapital)
                return false;

            return tradeAmountUsdt > 0;
        }

        public void RegisterTrade(decimal profitLoss)
        {
            _openPositions++;
            _dailyLoss += profitLoss;
            _lastTradeTime = DateTime.UtcNow;
        }

        public void ClosePosition()
        {
            if (_openPositions > 0)
                _openPositions--;
        }

        public string GetStatus()
        {
            return $"Posiciones: {_openPositions}/{_mutableSettings.MaxOpenPositions} | " +
                   $"Pérdida diaria: {_dailyLoss:F2} USDT | " +
                   $"Cooldown: {_mutableSettings.CooldownSeconds}s";
        }
    }
}
