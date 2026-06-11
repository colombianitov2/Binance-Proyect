using Binance.Net.Objects.Models.Spot;
using CryptoSelfBot.Engine.Models;
using CryptoSelfBot.Engine.Services;
using CryptoSelfBot.Wpf.Helpers;
using CryptoSelfBot.Wpf.Services;
using CryptoSelfBot.Wpf.Views;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CryptoAsset = CryptoSelfBot.Wpf.Models.CryptoAsset;
using FiatAsset = CryptoSelfBot.Wpf.Models.FiatAsset;

namespace CryptoSelfBot.Wpf
{
    public partial class MainWindow : Window
    {
        private BinanceService _binanceService = null!;
        private BinanceSocketService? _socketService;
        private RiskManager _riskManager = null!;
        private DatabaseService _databaseService = null!;
        private bool _isTrading;
        private CancellationTokenSource? _tradingCts;
        private readonly SynchronizationContext _uiContext = SynchronizationContext.Current!;
        private readonly Dictionary<string, decimal> _latestPrices = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _quoteAssets = { "USDT", "BTC", "ETH" };
        private Process? _pythonProcess;
        private static readonly System.Net.Http.HttpClient _sharedHttpClient = new System.Net.Http.HttpClient();

        private decimal _sellPercentage = 0.05m;
        private decimal _buyPercentage = 0.02m;
        private int _consecutiveSuccesses;
        private int _consecutiveFailures;
        private const decimal ScaleUpFactor = 1.5m;
        private const decimal ScaleDownFactor = 0.5m;
        private const decimal MaxSellPercentage = 0.25m;
        private const decimal MaxBuyPercentage = 0.10m;
        private const decimal MinPercentage = 0.01m;

        public MainWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Log("CryptoSelfBot iniciado. Esperando órdenes.", "INFO");

            // Wire header notifications
            try
            {
                if (this.FindName("HeaderControl") is CryptoSelfBot.Wpf.Components.Header.HeaderControl hdr)
                {
                    hdr.NotificationsClicked += (s, e) =>
                    {
                        var nw = new NotificationsWindow();
                        nw.Owner = this;
                        nw.Show();
                    };

            // Escuchar cambios en RiskSettings para actualizar RiskManager en runtime
            AppDataService.RiskSettingsChanged += () =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (_riskManager != null)
                        {
                            _riskManager.UpdateSettings(AppDataService.Configuration.RiskSettings);
                            Log("Configuración de riesgo actualizada en runtime.", "INFO");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("Error aplicando RiskSettings en runtime: " + ex.Message, "WARN");
                    }
                });
            };
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("MainWindow init error (header wiring): " + ex.Message); }

            // Escuchar cambios en la cuenta activa para actualizar servicios en tiempo de ejecución
            AppDataService.ActiveAccountChanged += () =>
            {
                this.Dispatcher.Invoke(async () =>
                {
                    if (_isTrading && _socketService != null && _binanceService != null)
                    {
                        var active = AppDataService.GetActiveAccount();
                        string apiKey = EncryptionHelper.Decrypt(active.ApiKey ?? "");
                        string secretKey = EncryptionHelper.Decrypt(active.SecretKey ?? "");
                        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(secretKey))
                        {
                            Log($"Cuenta activa cambiada a '{active.Label}' durante trading: actualizando credenciales.", "INFO");
                            try
                            {
                                _binanceService.UpdateCredentials(apiKey, secretKey);
                                await _socketService.UpdateCredentialsAsync(apiKey, secretKey);
                            }
                            catch (Exception ex)
                            {
                                Log("No se pudieron actualizar credenciales en vuelo: " + ex.Message, "WARN");
                            }
                        }
                    }
                });
            };

            // Wire CryptoCard remove events from the ListBox
            try
            {
                if (this.FindName("CryptoListBox") is System.Windows.Controls.ListBox lb)
                {
                    lb.AddHandler(CryptoSelfBot.Wpf.Components.Cards.CryptoCard.RemoveClickedEvent, new RoutedEventHandler((s, e) =>
                    {
                        // Forward to existing handler
                        RemoveCrypto_Click(s, (RoutedEventArgs)e);
                    }));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("MainWindow init error (crypto card wiring): " + ex.Message); }

            // Wire menu events
            try
            {
                if (this.FindName("AppMenu") is CryptoSelfBot.Wpf.Components.Menu.MenuControl menu)
                {
                    menu.AddHandler(CryptoSelfBot.Wpf.Components.Menu.MenuControl.HistoryClickedEvent, new RoutedEventHandler(History_Click));
                    menu.AddHandler(CryptoSelfBot.Wpf.Components.Menu.MenuControl.NewsClickedEvent, new RoutedEventHandler(News_Click));
                    menu.AddHandler(CryptoSelfBot.Wpf.Components.Menu.MenuControl.ExportClickedEvent, new RoutedEventHandler(Export_Click));
                    menu.AddHandler(CryptoSelfBot.Wpf.Components.Menu.MenuControl.SourcesClickedEvent, new RoutedEventHandler(Sources_Click));
                    menu.AddHandler(CryptoSelfBot.Wpf.Components.Menu.MenuControl.WithdrawClickedEvent, new RoutedEventHandler(Withdraw_Click));
                    menu.AddHandler(CryptoSelfBot.Wpf.Components.Menu.MenuControl.SettingsClickedEvent, new RoutedEventHandler(Settings_Click));
                    menu.AddHandler(CryptoSelfBot.Wpf.Components.Menu.MenuControl.InstructionsClickedEvent, new RoutedEventHandler(Instructions_Click));
                    menu.AddHandler(CryptoSelfBot.Wpf.Components.Menu.MenuControl.CreditsClickedEvent, new RoutedEventHandler(Credits_Click));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("MainWindow init error (menu wiring): " + ex.Message); }
        }

        public void Log(string message, string level = "INFO")
        {
            if (AppDataService.IsLoggingPaused()) return;
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"[{timestamp}] [{level}] {message}";
            _uiContext.Post(_ =>
            {
                try
                {
                    if (DataContext is ViewModels.MainViewModel vm)
                    {
                        vm.LogsList.Add(line);
                        vm.Notifications.Add(line);
                        while (vm.Notifications.Count > 200)
                            vm.Notifications.RemoveAt(0);
                        vm.Logs = line;
                    }
                    // Auto-scroll: seleccionar último
                    if (this.FindName("ConsoleLogListBox") is ListBox lb && lb.Items.Count > 0)
                    {
                        lb.SelectedIndex = lb.Items.Count - 1;
                        lb.ScrollIntoView(lb.SelectedItem);
                    }
                }
                catch { }
            }, null);
        }

        private void ForceRefreshPrices_Click(object sender, RoutedEventArgs e)
        {
            var vm = (ViewModels.MainViewModel)DataContext;
            vm.RefreshAllPrices();
            Log("Actualización manual de precios solicitada.", "INFO");
            _ = RefreshAccountBalancesAsync();
        }

        private async void ToggleTrading_Click(object sender, RoutedEventArgs e)
        {
            var vm = (ViewModels.MainViewModel)DataContext;
            if (_isTrading)
            {
                _isTrading = false;
                vm.IsTradingActive = false;
                vm.StatusMessage = "Trading detenido";
                vm.ConnectionStatus = "DESCONECTADO";
                _tradingCts?.Cancel();
                _tradingCts?.Dispose();
                _tradingCts = null;
                if (_socketService != null)
                {
                    await _socketService.DisconnectAsync();
                    _socketService = null;
                }
                StopPythonService();
                Log("Trading detenido y WebSocket desconectado.", "INFO");
                return;
            }

            EnsureActiveAccountHasCredentials();
            await StartPythonService();

            var activeAccount = AppDataService.GetActiveAccount();
            if (activeAccount.NoWithdrawals)
            {
                Log($"Cuenta activa '{activeAccount.Label}' tiene NoWithdrawals=true. Retiradas y operaciones sensibles estarán limitadas.", "WARN");
            }
            string apiKey = EncryptionHelper.Decrypt(activeAccount.ApiKey ?? "");
            string secretKey = EncryptionHelper.Decrypt(activeAccount.SecretKey ?? "");
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
            {
                vm.StatusMessage = "Cuenta sin credenciales. Configúrala en Configuración.";
                vm.ConnectionStatus = "ERROR";
                Log("ERROR: credenciales vacías en la cuenta activa.", "ERROR");
                return;
            }

            bool isTestnet = activeAccount.Environment == TradingEnvironment.Testnet;
            var env = isTestnet ? TradingEnvironment.Testnet : TradingEnvironment.Live;

            _binanceService = new BinanceService();
            bool conectado = await _binanceService.ConnectAsync(apiKey, secretKey, isTestnet);
            if (!conectado)
            {
                vm.StatusMessage = $"Error: {_binanceService.LastError}";
                vm.ConnectionStatus = "ERROR";
                Log($"ERROR al conectar: {_binanceService.LastError}", "ERROR");
                return;
            }

            var riskSettings = AppDataService.Configuration.RiskSettings;
            _riskManager = new RiskManager(riskSettings);

            _socketService = new BinanceSocketService(apiKey, secretKey, env);
            _socketService.PriceUpdated += OnPriceUpdated;
            var initialSymbols = new List<string> { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT" };
            await _socketService.SubscribeToTickersAsync(initialSymbols);
            Log($"WebSocket conectado a {initialSymbols.Count} pares.", "INFO");

            await _databaseService.InitializeAsync();
            await RefreshAccountBalancesAsync();
            _isTrading = true;
            _tradingCts = new CancellationTokenSource();
            vm.IsTradingActive = true;
            vm.ConnectionStatus = "CONECTADO";
            vm.StatusMessage = "Trading en curso (" + (isTestnet ? "Testnet" : "Real") + ")";
            Log($"Iniciando trading con cuenta '{activeAccount.Label}'...", "INFO");
            _ = EjecutarBucleDeTradingAsync(vm, _tradingCts.Token);
        }

        private void EnsureActiveAccountHasCredentials()
        {
            var config = AppDataService.Configuration;
            var active = AppDataService.GetActiveAccount();
            string apiKey = EncryptionHelper.Decrypt(active.ApiKey ?? "");
            string secretKey = EncryptionHelper.Decrypt(active.SecretKey ?? "");
            if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(secretKey)) return;
            for (int i = 0; i < config.Accounts.Count; i++)
            {
                var acc = config.Accounts[i];
                string key = EncryptionHelper.Decrypt(acc.ApiKey ?? "");
                string sec = EncryptionHelper.Decrypt(acc.SecretKey ?? "");
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(sec))
                {
                    AppDataService.SetActiveAccount(i);
                    Log($"Cuenta '{acc.Label}' activada automáticamente por tener credenciales.", "INFO");
                    return;
                }
            }
        }

        private async System.Threading.Tasks.Task StartPythonService()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string pythonFolder = AppDataService.Configuration.PythonPath ?? Path.Combine(baseDir, "PythonService");
                if (!Directory.Exists(pythonFolder))
                {
                    pythonFolder = @"D:\CryptoSelfBot.Python";
                    if (!Directory.Exists(pythonFolder))
                    {
                        Log("Carpeta Python no encontrada.", "WARN");
                        // No detener el inicio: permitir continuar sin Python, mostrar aviso en UI
                        return;
                    }
                }

                // Verificar si el puerto 5000 ya está en uso
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpListeners = ipProperties.GetActiveTcpListeners();
                if (tcpListeners.Any(e => e.Port == 5000))
                {
                    Log("El puerto 5000 ya está en uso. Se asume que el servicio Python ya está corriendo.", "WARN");
                    return;
                }

                _pythonProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = "-m uvicorn api:app --host 127.0.0.1 --port 5000",
                        WorkingDirectory = pythonFolder,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                _pythonProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log("PY: " + e.Data, "INFO"); };
                _pythonProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log("PYERR: " + e.Data, "WARN"); };

                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();

                // Simple healthcheck: esperar hasta 5s a que el puerto responda
                bool healthy = false;
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        _sharedHttpClient.Timeout = TimeSpan.FromSeconds(1);
                        var resp = await _sharedHttpClient.GetAsync("http://127.0.0.1:5000/");
                        if (resp.IsSuccessStatusCode)
                        {
                            healthy = true;
                            break;
                        }
                    }
                    catch { await Task.Delay(500); }
                }

                if (healthy) Log("Servicio Python iniciado y healthy.", "INFO");
                else Log("Servicio Python iniciado pero healthcheck falló (comprueba manualmente).", "WARN");
            }
            catch (Exception ex)
            {
                Log($"No se pudo iniciar Python: {ex.Message}", "WARN");
            }
        }

        private void StopPythonService()
        {
            try
            {
                if (_pythonProcess != null && !_pythonProcess.HasExited)
                {
                    _pythonProcess.Kill();
                    _pythonProcess.Dispose();
                    _pythonProcess = null;
                    Log("Servicio Python detenido.", "INFO");
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task RefreshOpenHistoryWindowsAsync()
        {
            await System.Threading.Tasks.Task.Yield();
            foreach (Window w in Application.Current.Windows)
            {
                if (w is Views.HistoryWindow hw)
                {
                    try { await hw.RefreshHistorialAsync(); } catch { }
                }
            }
        }

        private void OnPriceUpdated(string symbol, decimal price)
        {
            _uiContext.Post(_ =>
            {
                _latestPrices[symbol] = price;
                var vm = (ViewModels.MainViewModel)DataContext;
                // Extraer asset base probando sufijos conocidos para evitar reemplazos erróneos
                string baseAsset = string.Empty;
                foreach (var quote in _quoteAssets)
                {
                    if (symbol.EndsWith(quote, StringComparison.OrdinalIgnoreCase))
                    {
                        baseAsset = symbol.Substring(0, symbol.Length - quote.Length);
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(baseAsset))
                {
                    var existing = vm.CryptoAssets.FirstOrDefault(c => string.Equals(c.Symbol, baseAsset, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                        existing.Price = "$ " + price.ToString("F6");
                }
            }, null);
        }

        private async Task RefreshAccountBalancesAsync()
        {
            try
            {
                var vm = (ViewModels.MainViewModel)DataContext;
                if (_binanceService == null || !_binanceService.IsConnected)
                {
                    var activeAccount = AppDataService.GetActiveAccount();
                    string apiKey = EncryptionHelper.Decrypt(activeAccount.ApiKey ?? "");
                    string secretKey = EncryptionHelper.Decrypt(activeAccount.SecretKey ?? "");
                    if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
                    {
                        Log("No hay credenciales activas para sincronizar saldos.", "WARN");
                        return;
                    }

                    _binanceService = new BinanceService();
                    bool connected = await _binanceService.ConnectAsync(apiKey, secretKey, activeAccount.Environment == TradingEnvironment.Testnet);
                    if (!connected)
                    {
                        Log("No se pudo sincronizar Binance: " + _binanceService.LastError, "ERROR");
                        return;
                    }
                }

                var balances = await _binanceService.GetBalancesAsync();
                if (balances.Count == 0)
                {
                    Log("Binance respondió sin saldos positivos para mostrar.", "INFO");
                    return;
                }

                foreach (var balance in balances.OrderByDescending(b => b.Total))
                {
                    string asset = balance.Asset.ToUpperInvariant();
                    string amount = balance.Total.ToString("0.########");

                    if (IsFiatOrStable(asset))
                    {
                        vm.UpsertFiat(asset, amount);
                        Log($"Saldo fiat/stable detectado: {asset} = {amount}", "INFO");
                    }
                    else
                    {
                        decimal price = asset == "USDT" ? 1m : await _binanceService.GetPriceAsync(asset + "USDT");
                        string display = price > 0
                            ? $"{amount} | $ {price:0.######}"
                            : amount;
                        vm.UpsertCrypto(asset, display);
                        Log($"Saldo crypto detectado: {asset} = {amount}", "INFO");
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error sincronizando saldos: " + ex.Message, "ERROR");
            }
        }

        private static bool IsFiatOrStable(string asset)
        {
            string[] fiatOrStable =
            {
                "USDT", "USDC", "BUSD", "FDUSD", "TUSD", "DAI",
                "USD", "EUR", "COP", "ARS", "MXN", "GBP", "CAD", "AUD",
                "JPY", "CHF", "BRL", "CLP", "PEN", "UYU", "NZD", "SGD",
                "HKD", "DKK", "NOK"
            };
            return fiatOrStable.Contains(asset, StringComparer.OrdinalIgnoreCase);
        }

        private async Task EjecutarBucleDeTradingAsync(ViewModels.MainViewModel vm, CancellationToken token)
        {
            int ciclo = 0;
            Log("Bucle de trading iniciado.", "INFO");

            while (_isTrading && !token.IsCancellationRequested)
            {
                ciclo++;
                Log($"--- Ciclo {ciclo} ---", "INFO");
                bool cicloExitoso = false;
                try
                {
                    List<BinanceBalance> balances = await _binanceService.GetBalancesAsync();
                    Log($"Balances recibidos: {balances.Count} activos.", "INFO");

                    // No limpiamos las listas de watchlist, solo las actualizamos
                    // si el usuario ha agregado monedas manualmente.
                    // La watchlist se gestiona desde los botones "Agregar".

                    decimal totalCapitalUsdt = await CalculateTotalCapitalAsync(balances);
                    Log($"Capital total: {totalCapitalUsdt:F2} USDT | Venta: {_sellPercentage:P0} | Compra: {_buyPercentage:P0}", "INFO");

                    // VENTAS y COMPRAS (sin tocar las listas visuales aquí, solo la lógica de trading)
                    foreach (var crypto in vm.CryptoAssets.ToList())
                    {
                        string baseAsset = crypto.Symbol;
                        if (baseAsset == "USDT") continue;
                        string? bestPair = FindBestPair(baseAsset);
                        if (bestPair == null) continue;

                        var balanceInfo = balances.FirstOrDefault(b => b.Asset == baseAsset);
                        decimal quantity = balanceInfo?.Available ?? 0;
                        decimal price = _latestPrices.TryGetValue(bestPair, out var pr) ? pr : await _binanceService.GetPriceAsync(bestPair);
                        decimal notional = quantity * price;
                        decimal qtyToSell = quantity * _sellPercentage;

                        if (notional >= 10m && _riskManager.CanOpenTrade(notional, totalCapitalUsdt))
                        {
                            Log($"  >> VENDIENDO {qtyToSell:F6} {baseAsset} (Valor: {notional:F2} USDT)", "INFO");
                            bool ok = await _binanceService.PlaceMarketOrderAsync(bestPair, qtyToSell, false);
                            if (ok)
                            {
                                Log($"  ✅ Venta exitosa: {qtyToSell:F6} {baseAsset}", "SUCCESS");
                                _riskManager.RegisterTrade(0);
                                await _databaseService.InsertTradeAsync(new TradeRecord
                                {
                                    Symbol = bestPair,
                                    Side = "Sell",
                                    Price = price,
                                    Quantity = qtyToSell,
                                    TimestampUtc = DateTime.UtcNow,
                                    Strategy = "Autoescalado",
                                    Notes = $"Venta {_sellPercentage:P0} de {baseAsset}"
                                });
                                    // Refrescar ventanas de historial abiertas
                                    _ = RefreshOpenHistoryWindowsAsync();
                                cicloExitoso = true;
                            }
                            else Log($"  ❌ FALLO al vender {baseAsset}", "ERROR");
                        }
                    }

                    decimal usdtBalance = balances.FirstOrDefault(b => b.Asset == "USDT")?.Available ?? 0;
                    if (usdtBalance >= 10m)
                    {
                        var cryptosToBuy = vm.CryptoAssets
                            .Where(c => c.Symbol != "USDT")
                            .OrderByDescending(c => _latestPrices.TryGetValue(c.Symbol + "USDT", out var pr) ? pr : 0)
                            .Take(3)
                            .ToList();

                        foreach (var crypto in cryptosToBuy)
                        {
                            string pair = crypto.Symbol + "USDT";
                            decimal price = _latestPrices.TryGetValue(pair, out var prc) ? prc : await _binanceService.GetPriceAsync(pair);
                            if (price <= 0) continue;

                            decimal buyNotional = usdtBalance * _buyPercentage;
                            decimal qtyToBuy = buyNotional / price;

                            if (_riskManager.CanOpenTrade(buyNotional, totalCapitalUsdt))
                            {
                                Log($"  >> COMPRANDO {qtyToBuy:F6} {crypto.Symbol} (Invirtiendo {buyNotional:F2} USDT)", "INFO");
                                bool ok = await _binanceService.PlaceMarketOrderAsync(pair, qtyToBuy, true);
                                if (ok)
                                {
                                    Log($"  ✅ Compra exitosa: {qtyToBuy:F6} {crypto.Symbol}", "SUCCESS");
                                    _riskManager.RegisterTrade(0);
                                    await _databaseService.InsertTradeAsync(new TradeRecord
                                    {
                                        Symbol = pair,
                                        Side = "Buy",
                                        Price = price,
                                        Quantity = qtyToBuy,
                                        TimestampUtc = DateTime.UtcNow,
                                        Strategy = "Autoescalado",
                                        Notes = $"Compra por {buyNotional:F2} USDT de {crypto.Symbol}"
                                    });
                                    // Refrescar ventanas de historial abiertas
                                    _ = RefreshOpenHistoryWindowsAsync();
                                    cicloExitoso = true;
                                }
                                else Log($"  ❌ FALLO al comprar {crypto.Symbol}", "ERROR");
                            }
                        }
                    }

                    if (cicloExitoso)
                    {
                        _consecutiveSuccesses++;
                        _consecutiveFailures = 0;
                        if (_consecutiveSuccesses >= 3)
                        {
                            _sellPercentage = Math.Min(_sellPercentage * ScaleUpFactor, MaxSellPercentage);
                            _buyPercentage = Math.Min(_buyPercentage * ScaleUpFactor, MaxBuyPercentage);
                            Log($"  ↑ Racha positiva: Autoescalado aumentado a V:{_sellPercentage:P0} C:{_buyPercentage:P0}", "INFO");
                            _consecutiveSuccesses = 0;
                        }
                    }
                    else
                    {
                        _consecutiveFailures++;
                        _consecutiveSuccesses = 0;
                        if (_consecutiveFailures >= 3)
                        {
                            _sellPercentage = Math.Max(_sellPercentage * ScaleDownFactor, MinPercentage);
                            _buyPercentage = Math.Max(_buyPercentage * ScaleDownFactor, MinPercentage);
                            Log($"  ↓ Racha negativa: Autoescalado reducido a V:{_sellPercentage:P0} C:{_buyPercentage:P0}", "INFO");
                            _consecutiveFailures = 0;
                        }
                    }

                    vm.StatusMessage = cicloExitoso ? "Operaciones ejecutadas." : "Sin operaciones.";
                }
                catch (Exception ex)
                {
                    Log($"EXCEPCIÓN en ciclo {ciclo}: {ex.Message}", "ERROR");
                    vm.StatusMessage = $"Error en ciclo: {ex.Message}";
                }
                try
                {
                    await Task.Delay(500, token);
                }
                catch (TaskCanceledException) { break; }
            }
            Log("Bucle de trading finalizado.", "INFO");
        }

        private async Task<decimal> CalculateTotalCapitalAsync(List<BinanceBalance> balances)
        {
            decimal total = 0;
            foreach (var b in balances)
            {
                if (b.Asset == "USDT") total += b.Total;
                else
                {
                    string pair = b.Asset + "USDT";
                    decimal price = _latestPrices.TryGetValue(pair, out var p) ? p : await _binanceService.GetPriceAsync(pair);
                    total += b.Total * price;
                }
            }
            return total;
        }

        private string? FindBestPair(string baseAsset)
        {
            foreach (var quote in _quoteAssets)
            {
                if (quote == baseAsset) continue;
                string pair = baseAsset + quote;
                if (_latestPrices.TryGetValue(pair, out _)) return pair;
            }
            return null;
        }

        // Métodos de menú
        private void History_Click(object sender, RoutedEventArgs e) =>
            new HistoryWindow { Owner = this }.ShowDialog();
        private void News_Click(object sender, RoutedEventArgs e) =>
            new NewsWindow { Owner = this }.ShowDialog();

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dbService = new DatabaseService();
                await dbService.InitializeAsync();
                var trades = await dbService.GetTradesAsync();
                if (trades.Count == 0)
                {
                    MessageBox.Show("No hay operaciones para exportar.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string exportsFolder = Path.Combine(AppDataService.Configuration.StoragePath, "Exports");
                Directory.CreateDirectory(exportsFolder);
                string fileName = $"Historial_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx";
                string filePath = Path.Combine(exportsFolder, fileName);

                ExcelPackage.License.SetNonCommercialPersonal("CryptoSelfBot");

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Operaciones");
                worksheet.Cells[1, 1].Value = "Fecha";
                worksheet.Cells[1, 2].Value = "Par";
                worksheet.Cells[1, 3].Value = "Tipo";
                worksheet.Cells[1, 4].Value = "Cantidad";
                worksheet.Cells[1, 5].Value = "Precio";
                worksheet.Cells[1, 6].Value = "Estrategia";
                worksheet.Cells[1, 7].Value = "Notas";
                for (int i = 0; i < trades.Count; i++)
                {
                    worksheet.Cells[i + 2, 1].Value = trades[i].TimestampUtc.ToString("dd/MM/yyyy HH:mm:ss");
                    worksheet.Cells[i + 2, 2].Value = trades[i].Symbol;
                    worksheet.Cells[i + 2, 3].Value = trades[i].Side;
                    worksheet.Cells[i + 2, 4].Value = trades[i].Quantity;
                    worksheet.Cells[i + 2, 5].Value = trades[i].Price;
                    worksheet.Cells[i + 2, 6].Value = trades[i].Strategy;
                    worksheet.Cells[i + 2, 7].Value = trades[i].Notes;
                }
                await package.SaveAsAsync(new FileInfo(filePath));
                Log("Exportación Excel completada.", "INFO");
                MessageBox.Show($"Archivo exportado correctamente:\n{filePath}", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"ERROR al exportar: {ex.Message}", "ERROR");
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Sources_Click(object sender, RoutedEventArgs e) =>
            new SourcesWindow { Owner = this }.ShowDialog();
        private void Withdraw_Click(object sender, RoutedEventArgs e) =>
            new WithdrawWindow { Owner = this }.ShowDialog();
        private void Settings_Click(object sender, RoutedEventArgs e) =>
            new SettingsWindow { Owner = this }.ShowDialog();
        private void Instructions_Click(object sender, RoutedEventArgs e) =>
            new InstructionsWindow { Owner = this }.ShowDialog();
        private void Credits_Click(object sender, RoutedEventArgs e) =>
            new CreditsWindow { Owner = this }.ShowDialog();
        private void NotificationsButton_Click(object sender, MouseButtonEventArgs e) =>
            new NotificationsWindow { Owner = this }.ShowDialog();

        // Handlers para botones Start/Stop que delegan en ToggleTrading_Click
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Si ya está en trading, no hacemos nada
            if (_isTrading)
            {
                Log("Intento de iniciar trading cuando ya está activo.", "WARN");
                return;
            }

            // Reusar la lógica existente de ToggleTrading_Click para iniciar trading
            try
            {
                ToggleTrading_Click(sender, e);
            }
            catch (Exception ex)
            {
                Log("Error al iniciar trading desde StartButton_Click: " + ex.Message, "ERROR");
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Si no está en trading, no hacemos nada
            if (!_isTrading)
            {
                Log("Intento de detener trading cuando no está activo.", "WARN");
                return;
            }

            try
            {
                ToggleTrading_Click(sender, e);
            }
            catch (Exception ex)
            {
                Log("Error al detener trading desde StopButton_Click: " + ex.Message, "ERROR");
            }
        }

        private void AddCrypto_Click(object sender, RoutedEventArgs e)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Ingresa el símbolo de la criptomoneda (ej: BTC, ETH, SOL):",
                "Agregar Crypto", "BTC");
            if (!string.IsNullOrWhiteSpace(input))
            {
                var vm = (ViewModels.MainViewModel)DataContext;
                string symbol = input.Trim().ToUpper();
                if (!vm.CryptoAssets.Any(c => c.Symbol == symbol))
                {
                    vm.CryptoAssets.Add(new CryptoAsset
                    {
                        Symbol = symbol,
                        Price = "Cargando...",
                        Change = "",
                        IsNegative = false
                    });
                    Log($"Crypto agregada a watchlist: {symbol}", "INFO");
                }
            }
        }

        private void AddFiat_Click(object sender, RoutedEventArgs e)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Ingresa el código de la moneda fiat (ej: USD, EUR, COP):",
                "Agregar Fiat", "USD");
            if (!string.IsNullOrWhiteSpace(input))
            {
                var vm = (ViewModels.MainViewModel)DataContext;
                string symbol = input.Trim().ToUpper();
                if (!vm.FiatAssets.Any(f => f.Symbol == symbol))
                {
                    vm.FiatAssets.Add(new FiatAsset { Symbol = symbol, Value = "Cargando..." });
                    Log($"Fiat agregada a watchlist: {symbol}", "INFO");
                }
            }
        }

        private void RemoveCrypto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CryptoAsset asset)
                ((ViewModels.MainViewModel)DataContext).CryptoAssets.Remove(asset);
        }
        private void RemoveFiat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FiatAsset asset)
                ((ViewModels.MainViewModel)DataContext).FiatAssets.Remove(asset);
        }
    }
}
