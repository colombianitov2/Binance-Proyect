using CryptoSelfBot.Engine.Models;
using CryptoSelfBot.Engine.Services;
using CryptoSelfBot.Wpf.Helpers;
using CryptoSelfBot.Wpf.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace CryptoSelfBot.Wpf.Views
{
    public partial class WithdrawWindow : Window
    {
        private readonly BinanceService _binanceService;
        private readonly string _targetCurrency = "USDT";
        private static readonly HttpClient _pythonClient = new() { BaseAddress = new Uri("http://127.0.0.1:5000") };

        public WithdrawWindow()
        {
            InitializeComponent();
            _binanceService = new BinanceService();
        }

        private async void Consolidate_Click(object sender, RoutedEventArgs e)
        {
            var activeAccount = AppDataService.GetActiveAccount();
            string apiKey = EncryptionHelper.Decrypt(activeAccount.ApiKey);
            string secretKey = EncryptionHelper.Decrypt(activeAccount.SecretKey);
            bool isTestnet = activeAccount.Environment == TradingEnvironment.Testnet;

            if (!await _binanceService.ConnectAsync(apiKey, secretKey, isTestnet))
            {
                MessageBox.Show("Error al conectar con Binance: " + _binanceService.LastError);
                return;
            }
            var balances = await _binanceService.GetBalancesAsync();
            string targetAsset = (WithdrawCurrencyCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _targetCurrency;

            // Consolidar: vender todo lo que no sea la moneda objetivo
            bool anyOperation = false;
            foreach (var balance in balances.Where(b => b.Asset != targetAsset && b.Total > 0))
            {
                // Estimación previa usando el servicio local Python (/convert)
                decimal? estimatedTarget = null;
                string[] route = Array.Empty<string>();
                try
                {
                    var payload = new { from_symbol = balance.Asset, to_symbol = targetAsset, amount = (double)balance.Available };
                    var resp = await _pythonClient.PostAsJsonAsync("/convert", payload);
                    if (resp.IsSuccessStatusCode)
                    {
                        var txt = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(txt);
                        if (doc.RootElement.TryGetProperty("converted_amount", out var convEl) && convEl.ValueKind != JsonValueKind.Null)
                        {
                            if (convEl.TryGetDouble(out var d)) estimatedTarget = (decimal)d;
                        }
                        if (doc.RootElement.TryGetProperty("route", out var routeEl) && routeEl.ValueKind == JsonValueKind.Array)
                        {
                            route = routeEl.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
                        }
                    }
                }
                catch
                {
                    // si falla el servicio Python, continuamos con la lógica directa
                }

                if (estimatedTarget != null)
                {
                    var res = MessageBox.Show($"Se estima {estimatedTarget.Value:F6} {targetAsset} por vender {balance.Available} {balance.Asset}.\nRuta: {string.Join(" -> ", route)}\n\n¿Deseas continuar con la operación?",
                        "Confirmar consolidación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) continue;
                }

                string symbol = balance.Asset + targetAsset;
                // Intentar vender por completo usando orden market
                try
                {
                    bool success = false;
                    // Intentar usar precio directo para validar el par
                    var directPrice = await _binanceService.GetPriceAsync(symbol);
                    if (directPrice > 0)
                    {
                        success = await _binanceService.PlaceMarketOrderAsync(symbol, balance.Available, false);
                        if (success)
                        {
                            anyOperation = true;
                            Console.WriteLine($"Vendido {balance.Available} {balance.Asset} -> {targetAsset}");
                        }
                    }

                    if (!success)
                    {
                        // Si falla o no hay par directo, intentar con BTC como intermediario
                        if (balance.Asset != "BTC")
                        {
                            string intermediateSymbol = balance.Asset + "BTC";
                            var interPrice = await _binanceService.GetPriceAsync(intermediateSymbol);
                            if (interPrice > 0)
                            {
                                bool intermediateSuccess = await _binanceService.PlaceMarketOrderAsync(intermediateSymbol, balance.Available, false);
                                if (intermediateSuccess)
                                {
                                    anyOperation = true;
                                    Console.WriteLine($"Vendido {balance.Available} {balance.Asset} -> BTC");
                                    // Intentar vender BTC a targetAsset si es necesario
                                    if (targetAsset != "BTC")
                                    {
                                        // Aproximación: convertir el monto obtenido en BTC usando el precio intermedio
                                        decimal btcAmount = balance.Available * interPrice;
                                        string btcToTarget = "BTC" + targetAsset;
                                        var btcToTargetPrice = await _binanceService.GetPriceAsync(btcToTarget);
                                        if (btcToTargetPrice > 0 && btcAmount > 0)
                                        {
                                            await _binanceService.PlaceMarketOrderAsync(btcToTarget, btcAmount, false);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al vender {balance.Asset}: {ex.Message}");
                }
            }

            if (anyOperation)
            {
                MessageBox.Show("Consolidación completada. Los fondos han sido convertidos a " + targetAsset + ".",
                    "Retiro", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No se encontraron activos para consolidar o ya están en " + targetAsset + ".",
                    "Retiro", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            Close();
        }
    }
}