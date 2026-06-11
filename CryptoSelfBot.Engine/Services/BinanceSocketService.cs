using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;
using CryptoSelfBot.Engine.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public class BinanceSocketService
    {
        private BinanceSocketClient? _socketClient;
        public bool IsConnected { get; private set; }
        public event Action<string, decimal>? PriceUpdated;
        public event Action<bool>? ConnectionStateChanged;

        private readonly int _maxRetries = 5;
        private readonly TimeSpan _baseDelay = TimeSpan.FromSeconds(2);

        private readonly string? _apiKey;
        private readonly string? _apiSecret;
        private readonly TradingEnvironment _environment;
        private IEnumerable<string>? _subscribedSymbols;
        private CancellationTokenSource? _reconnectCts;

        public BinanceSocketService(string apiKey, string apiSecret, TradingEnvironment environment = TradingEnvironment.Testnet)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _environment = environment;
            CreateClient();
        }

        public async Task UpdateCredentialsAsync(string apiKey, string apiSecret)
        {
            // Actualizar credenciales y recrear cliente
            try
            {
                // Cambiar campos internos mediante reflexión no necesaria, simplemente recrear cliente con nuevos valores
                var newService = new BinanceSocketService(apiKey, apiSecret, _environment);
                // Transferir estado de suscripciones
                if (_subscribedSymbols != null)
                {
                    await newService.SubscribeToTickersAsync(_subscribedSymbols);
                }

                // Reemplazar internals
                this.DisconnectAsync().Wait();
                this._socketClient = newService._socketClient;
                this.IsConnected = newService.IsConnected;
                this._reconnectCts = newService._reconnectCts;
            }
            catch { }
        }

        private void CreateClient()
        {
            _socketClient?.Dispose();
            _socketClient = new BinanceSocketClient(options =>
            {
                options.Environment = _environment == TradingEnvironment.Testnet
                    ? BinanceEnvironment.Testnet
                    : BinanceEnvironment.Live;

                if (!string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_apiSecret))
                {
                    options.ApiCredentials = new BinanceCredentials(_apiKey, _apiSecret);
                }
            });
        }

        public async Task SubscribeToTickersAsync(IEnumerable<string> symbols)
        {
            if (_socketClient == null)
                throw new InvalidOperationException("Socket client no inicializado.");

            // Guardar símbolos suscritos para reconexión automática
            _subscribedSymbols = symbols.ToList();
            _reconnectCts?.Cancel();
            _reconnectCts = null;

            // Intentamos suscribirnos a cada símbolo con reintentos y backoff exponencial.
            foreach (var symbol in _subscribedSymbols)
            {
                int attempt = 0;
                bool subscribed = false;
                string lastError = string.Empty;

                while (attempt < _maxRetries && !subscribed)
                {
                    try
                    {
                        var result = await _socketClient.SpotApi.ExchangeData.SubscribeToTickerUpdatesAsync(
                            symbol,
                            data =>
                            {
                                if (data?.Data != null)
                                {
                                    PriceUpdated?.Invoke(symbol, data.Data.LastPrice);
                                }
                            });

                        if (result.Success)
                        {
                            subscribed = true;
                            break;
                        }

                        lastError = result.Error?.Message ?? "Error desconocido al suscribirse";
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                    }

                    attempt++;
                    var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay);
                }

                if (!subscribed)
                {
                    // Si no pudimos suscribirnos a un símbolo inicial, lanzar excepción para que el llamador lo sepa
                    throw new Exception($"Error al suscribirse a {symbol}: {lastError}");
                }
            }

            IsConnected = true;
            ConnectionStateChanged?.Invoke(true);

            // Iniciar loop de reconexión en background para supervisar desconexiones
            StartReconnectLoop();
        }

        public async Task DisconnectAsync()
        {
            if (_socketClient != null)
                await _socketClient.UnsubscribeAllAsync();
            IsConnected = false;
            ConnectionStateChanged?.Invoke(false);
            // Cancelar intento de reconexión si existe
            try { _reconnectCts?.Cancel(); } catch { }
            _reconnectCts = null;
        }

        private void StartReconnectLoop()
        {
            // Cancelar si ya hay uno
            if (_reconnectCts != null) return;
            _reconnectCts = new CancellationTokenSource();
            var ct = _reconnectCts.Token;
            Task.Run(async () =>
            {
                int attempt = 0;
                while (!ct.IsCancellationRequested)
                {
                    if (IsConnected)
                    {
                        await Task.Delay(1000, ct).ConfigureAwait(false);
                        continue;
                    }

                    attempt++;
                    try
                    {
                        // Re-crear cliente y re-suscribir
                        CreateClient();
                        if (_subscribedSymbols != null)
                        {
                            await SubscribeToTickersAsync(_subscribedSymbols);
                        }
                    }
                    catch
                    {
                        // No hacer throw, esperar y reintentar
                    }

                    var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempt - 1, 6)));
                    try { await Task.Delay(delay, ct); } catch { break; }
                }
            }, ct);
        }
    }
}