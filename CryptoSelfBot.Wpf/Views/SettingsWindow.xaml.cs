using CryptoSelfBot.Engine.Models;
using CryptoSelfBot.Engine.Services;
using CryptoSelfBot.Updater;
using CryptoSelfBot.Wpf.Helpers;
using CryptoSelfBot.Wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CryptoSelfBot.Wpf.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly BinanceService _binanceService;
        private readonly CoolorsService _coolors = new();
        private readonly ColormindService _colormind = new();
        private const string MaskedSecret = "********";

        public SettingsWindow()
        {
            InitializeComponent();
            MaxTradeAmountBox.TextChanged += (_, _) => UpdateRiskPreviewTexts();
            _binanceService = new BinanceService();
            LoadAll();
            LoadVersionInfo();
        }

        private void ExportAccounts_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "JSON Files|*.json", FileName = "accounts.json" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    AppDataService.ExportAccounts(dlg.FileName);
                    AccountStatusText.Text = "Cuentas exportadas.";
                }
                catch (Exception ex)
                {
                    AccountStatusText.Text = "Error exportando: " + ex.Message;
                }
            }
        }

        private void PauseLoggingCheck_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                AppDataService.SetPauseLogging(PauseLoggingCheck.IsChecked == true);
                AccountStatusText.Text = PauseLoggingCheck.IsChecked == true ? "Registro pausado." : "Registro activo.";
            }
            catch (Exception ex)
            {
                AccountStatusText.Text = "Error guardando configuración: " + ex.Message;
            }
        }

        private void ImportAccounts_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON Files|*.json" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    AppDataService.ImportAccounts(dlg.FileName);
                    LoadAccounts();
                    AccountStatusText.Text = "Cuentas importadas.";
                }
                catch (Exception ex)
                {
                    AccountStatusText.Text = "Error importando: " + ex.Message;
                }
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            int idx = AccountsListBox.SelectedIndex;
            if (idx < 0)
            {
                ApiGuideStatusText.Text = "Selecciona una cuenta para probar.";
                return;
            }

            var acc = AppDataService.Configuration.Accounts[idx];
            string apiKey = "";
            string secret = "";
            try { apiKey = EncryptionHelper.Decrypt(acc.ApiKey); } catch { }
            try { secret = EncryptionHelper.Decrypt(acc.SecretKey); } catch { }

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secret))
            {
                ApiGuideStatusText.Text = "La cuenta no tiene claves válidas.";
                return;
            }

            ApiGuideStatusText.Text = "Probando conexión...";
            var svc = new BinanceService();
            bool ok = await svc.ConnectAsync(apiKey, secret, acc.Environment == TradingEnvironment.Testnet);
            if (ok) ApiGuideStatusText.Text = "Conexión OK. Credenciales válidas.";
            else ApiGuideStatusText.Text = "Error: " + svc.LastError;
        }

        private void ForceMigrate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppDataService.ForceMigrateAccountKeysToDpapi();
                ApiGuideStatusText.Text = "Migración completada. Las claves ahora están cifradas.";
            }
            catch (Exception ex)
            {
                ApiGuideStatusText.Text = "Error durante migración: " + ex.Message;
            }
        }

        private void AccountsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = AccountsListBox.SelectedIndex;
            if (idx < 0 || idx >= AppDataService.Configuration.Accounts.Count)
            {
                ClearFields();
                return;
            }

            var acc = AppDataService.Configuration.Accounts[idx];
            AccountNameBox.Text = acc.Label;
            try
            {
                ApiKeyBox.Password = string.IsNullOrEmpty(acc.ApiKey) ? "" : MaskedSecret;
            }
            catch { ApiKeyBox.Password = ""; }

            try
            {
                SecretKeyBox.Password = string.IsNullOrEmpty(acc.SecretKey) ? "" : MaskedSecret;
            }
            catch { SecretKeyBox.Password = ""; }

            EnvironmentCombo.SelectedIndex = acc.Environment == TradingEnvironment.Testnet ? 1 : 0;
            NoWithdrawalsCheck.IsChecked = acc.NoWithdrawals;
        }

        private void LoadAll()
        {
            LoadAccounts();
            LoadRiskSettings();
            LoadStoragePath();
            LoadMarketSelection();
            LoadPythonPath();
            LoadApiSettings();
        }

        private void LoadApiSettings()
        {
            FREDKeyBox.Text = AppDataService.Configuration.FREDApiKey ?? "";
            EcbScraperCheck.IsChecked = AppDataService.Configuration.EcbScraperEnabled;
        }

        private void LoadMarketSelection()
        {
            MarketCombo.SelectedIndex = 0;
        }

        // =================================================================
        // CUENTAS (lógica simplificada)
        // =================================================================
        private void LoadAccounts()
        {
            var config = AppDataService.Configuration;
            AccountsListBox.ItemsSource = null;
            AccountsListBox.ItemsSource = config.Accounts;
            AccountsListBox.DisplayMemberPath = "Label";
            AccountsListBox.SelectedIndex = config.ActiveAccountIndex;
            ClearFields();
        }

        private void ClearFields()
        {
            AccountNameBox.Text = "Nueva cuenta";
            ApiKeyBox.Password = "";
            SecretKeyBox.Password = "";
            EnvironmentCombo.SelectedIndex = 0;
            NoWithdrawalsCheck.IsChecked = false;
        }

        private void SaveAccountChanges_Click(object sender, RoutedEventArgs e)
        {
            string name = AccountNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                AccountStatusText.Text = "❌ Debes asignar un nombre a la cuenta.";
                return;
            }

            string apiKey = ApiKeyBox.Password.Trim();
            string secretKey = SecretKeyBox.Password.Trim();
            TradingEnvironment env = ((ComboBoxItem)EnvironmentCombo.SelectedItem)?.Tag?.ToString() == "Testnet"
                ? TradingEnvironment.Testnet : TradingEnvironment.Live;

            bool noWithdrawals = NoWithdrawalsCheck.IsChecked == true;

            var config = AppDataService.Configuration;
            int selectedIdx = AccountsListBox.SelectedIndex;

            if (selectedIdx >= 0 && selectedIdx < config.Accounts.Count)
            {
                var account = config.Accounts[selectedIdx];
                account.Label = name;
                if (apiKey != MaskedSecret)
                    account.ApiKey = EncryptionHelper.Encrypt(apiKey);
                if (secretKey != MaskedSecret)
                    account.SecretKey = EncryptionHelper.Encrypt(secretKey);
                account.NoWithdrawals = noWithdrawals;
                account.Environment = env;
                AccountStatusText.Text = $"✅ Cuenta '{name}' actualizada.";
            }
            else
            {
                var newAccount = new BinanceAccount
                {
                    Label = name,
                    ApiKey = EncryptionHelper.Encrypt(apiKey),
                    SecretKey = EncryptionHelper.Encrypt(secretKey),
                    NoWithdrawals = noWithdrawals,
                    Environment = env
                };
                config.Accounts.Add(newAccount);
                config.ActiveAccountIndex = config.Accounts.Count - 1;
                AccountStatusText.Text = $"✅ Cuenta '{name}' agregada y activada.";
            }

            AppDataService.Save();
            LoadAccounts();

            AppDataService.Configuration.MarketType = MarketType.Spot;
            AppDataService.Save();
        }

        private void ActivateAccount_Click(object sender, RoutedEventArgs e)
        {
            int idx = AccountsListBox.SelectedIndex;
            if (idx < 0) return;
            AppDataService.SetActiveAccount(idx);
            LoadAccounts();
            AccountStatusText.Text = "Cuenta activada.";
        }

        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            int idx = AccountsListBox.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show("Selecciona una cuenta para eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AppDataService.Configuration.Accounts.RemoveAt(idx);
            if (AppDataService.Configuration.ActiveAccountIndex >= AppDataService.Configuration.Accounts.Count)
                AppDataService.Configuration.ActiveAccountIndex = Math.Max(0, AppDataService.Configuration.Accounts.Count - 1);

            AppDataService.Save();
            LoadAccounts();
            AccountStatusText.Text = "Cuenta eliminada.";
        }

        // =================================================================
        // TEMA
        // =================================================================
        private void ApplyTheme_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeListBox.SelectedItem is ListBoxItem item && item.Tag != null)
            {
                ThemeManager.ApplyTheme(item.Tag.ToString()!);
                ThemeStatusText.Text = $"Tema '{item.Content}' aplicado.";
            }
            else
            {
                ThemeStatusText.Text = "Selecciona un tema de la lista.";
            }
        }

        private async void SurpriseTheme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ThemeStatusText.Text = "Obteniendo paleta de Colormind...";
                // Log to main console
                try { if (Application.Current.MainWindow is MainWindow mw) mw.Log("Intentando obtener paleta de Colormind..."); } catch { }
                var cm = await _colormind.GetRandomPaletteAsync();
                if (cm != null && cm.Count >= 5)
                {
                    AplicarPaleta(cm);
                    ThemeStatusText.Text = "✅ Paleta Colormind aplicada.";
                    try { if (Application.Current.MainWindow is MainWindow mw) mw.Log("Paleta obtenida de Colormind."); } catch { }
                    return;
                }
                // Fallback a Coolors
                ThemeStatusText.Text = "Colormind falló, intentando Coolors...";
                try { if (Application.Current.MainWindow is MainWindow mw) mw.Log("Colormind falló. Intentando Coolors..."); } catch { }
                var colors = await _coolors.GetRandomPaletteAsync();
                if (colors != null && colors.Count >= 5)
                {
                    AplicarPaleta(colors);
                    ThemeStatusText.Text = "✅ Paleta Coolors aplicada.";
                    try { if (Application.Current.MainWindow is MainWindow mw) mw.Log("Paleta obtenida de Coolors."); } catch { }
                }
                else
                {
                    try { if (Application.Current.MainWindow is MainWindow mw) mw.Log("Coolors falló. Generando paleta local..."); } catch { }
                    // fallback local
                    colors = GenerarPaletaLocal();
                    AplicarPaleta(colors);
                    ThemeStatusText.Text = "✅ Paleta generada localmente.";
                    try { if (Application.Current.MainWindow is MainWindow mw) mw.Log("Paleta generada localmente."); } catch { }
                }
            }
            catch
            {
                // fallback a generador local
                var l = GenerarPaletaLocal();
                AplicarPaleta(l);
                ThemeStatusText.Text = "✅ Paleta generada localmente (fallback).";
                try { if (Application.Current.MainWindow is MainWindow mw) mw.Log("Error inesperado: usando generador local."); } catch { }
            }
        }

        private List<System.Windows.Media.Color> GenerarPaletaLocal()
        {
            var rnd = new System.Random();
            // Generar color base en HSV y derivar tres variaciones
            double h = rnd.NextDouble() * 360.0;
            double s = 0.6 + rnd.NextDouble() * 0.4; // 0.6 - 1.0
            double v = 0.45 + rnd.NextDouble() * 0.35; // 0.45 - 0.8

            var baseColor = HsvToColor(h, s, v);
            var c1 = baseColor;
            var c2 = HsvToColor((h + 30) % 360, Math.Min(1.0, s + 0.1), Math.Max(0.2, v - 0.1));
            var c3 = HsvToColor((h + 200) % 360, Math.Max(0.2, s - 0.2), Math.Min(1.0, v + 0.1));
            var c4 = HsvToColor((h + 90) % 360, Math.Min(1.0, s + 0.05), Math.Max(0.2, v - 0.2));
            var c5 = HsvToColor((h + 270) % 360, Math.Max(0.15, s - 0.3), Math.Min(1.0, v + 0.2));
            return new List<System.Windows.Media.Color> { c1, c2, c3, c4, c5 };
        }

        private System.Windows.Media.Color HsvToColor(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            byte R = (byte)Math.Round((r + m) * 255);
            byte G = (byte)Math.Round((g + m) * 255);
            byte B = (byte)Math.Round((b + m) * 255);
            return System.Windows.Media.Color.FromRgb(R, G, B);
        }

        private void AplicarPaleta(List<System.Windows.Media.Color> colors)
        {
            if (colors.Count < 5) return;
            var app = Application.Current;
            // Map palette to centralized resource keys (Colors.xaml)
            app.Resources["WindowBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(colors[0]);
            app.Resources["CardBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(colors[1]);
            app.Resources["CardBrush"] = new System.Windows.Media.SolidColorBrush(colors[1]);
            app.Resources["PrimaryBrush"] = new System.Windows.Media.SolidColorBrush(colors[2]);
            app.Resources["TextBrush"] = new System.Windows.Media.SolidColorBrush(colors[3]);
            app.Resources["MutedTextBrush"] = new System.Windows.Media.SolidColorBrush(colors[4]);
            app.Resources["BackgroundBrush"] = new System.Windows.Media.SolidColorBrush(colors[0]);

            // Computed variants
            app.Resources["BorderBrushColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(
                (byte)(colors[1].R + 30 > 255 ? 255 : colors[1].R + 30),
                (byte)(colors[1].G + 30 > 255 ? 255 : colors[1].G + 30),
                (byte)(colors[1].B + 30 > 255 ? 255 : colors[1].B + 30)));

            app.Resources["ContentBackground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(
                (byte)(colors[0].R + 20 > 255 ? 255 : colors[0].R + 20),
                (byte)(colors[0].G + 20 > 255 ? 255 : colors[0].G + 20),
                (byte)(colors[0].B + 20 > 255 ? 255 : colors[0].B + 20)));

            // Keep header border in sync
            app.Resources["HeaderBorder"] = app.Resources["BorderBrushColor"];
        }

        private void OpenThemeWindow_Click(object sender, RoutedEventArgs e)
        {
            new ThemeWindow { Owner = this }.ShowDialog();
        }

        // =================================================================
        // RIESGO
        // =================================================================
        private void LoadRiskSettings()
        {
            var risk = AppDataService.Configuration.RiskSettings;
            MaxTradeAmountBox.Text = risk.AutoMaxTradeAmount ? "" : risk.MaxTradeAmountUsdt.ToString("0.##");
            MaxPosSlider.Value = risk.MaxOpenPositions;
            MaxDailyLossSlider.Value = (double)risk.MaxDailyLossPercent;
            CooldownSlider.Value = risk.CooldownSeconds;

            UpdateRiskPreviewTexts();
        }

        private void SaveRiskSettings_Click(object sender, RoutedEventArgs e)
        {
            decimal fixedMaxTrade = 0m;
            bool autoMaxTrade = string.IsNullOrWhiteSpace(MaxTradeAmountBox.Text);
            if (!autoMaxTrade && !decimal.TryParse(MaxTradeAmountBox.Text.Trim(), out fixedMaxTrade))
            {
                RiskStatusText.Text = "Máximo por operación debe ser numérico o quedar vacío para automático.";
                return;
            }

            var risk = new RiskSettings
            {
                AutoMaxTradeAmount = autoMaxTrade,
                MaxTradeAmountUsdt = autoMaxTrade ? 0m : fixedMaxTrade,
                MaxOpenPositions = (int)MaxPosSlider.Value,
                MaxDailyLossPercent = (decimal)MaxDailyLossSlider.Value,
                CooldownSeconds = (int)CooldownSlider.Value,
                EnableTrading = true
            };
            AppDataService.SaveRiskSettings(risk);
            LoadRiskSettings();
            RiskStatusText.Text = "Configuración de riesgo guardada.";
        }

        private void RiskSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            UpdateRiskPreviewTexts();
        }

        private void UpdateRiskPreviewTexts()
        {
            if (MaxTradeValueText == null) return;
            MaxTradeValueText.Text = string.IsNullOrWhiteSpace(MaxTradeAmountBox?.Text)
                ? "Automático si se deja vacío"
                : $"{MaxTradeAmountBox.Text.Trim()} USDT";
            MaxPosValueText.Text = $"{(int)MaxPosSlider.Value} posiciones";
            MaxDailyLossValueText.Text = $"{(decimal)MaxDailyLossSlider.Value:0}%";
            CooldownValueText.Text = $"{(int)CooldownSlider.Value} s";
        }

        private void SaveApiSettings_Click(object sender, RoutedEventArgs e)
        {
            AppDataService.Configuration.FREDApiKey = FREDKeyBox.Text.Trim();
            AppDataService.Configuration.EcbScraperEnabled = EcbScraperCheck.IsChecked == true;
            AppDataService.Save();
            ApiGuideStatusText.Text = "Configuración de API guardada.";
        }

        // =================================================================
        // ALMACENAMIENTO
        // =================================================================
        private void LoadStoragePath()
        {
            StoragePathText.Text = AppDataService.Configuration.StoragePath;
        }

        private void LoadPythonPath()
        {
            PythonPathText.Text = AppDataService.Configuration.PythonPath ?? "";
        }

        private void SelectPythonFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Selecciona la carpeta de Python/Servicio Python" };
            if (dialog.ShowDialog() == true)
            {
                string newPath = dialog.FolderName;
                AppDataService.Configuration.PythonPath = newPath;
                AppDataService.Save();
                PythonPathText.Text = newPath;
                PythonStatusText.Text = "✅ Ruta de servicio integrado actualizada.";
            }
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Selecciona la carpeta de almacenamiento" };
            if (dialog.ShowDialog() == true)
            {
                string newPath = dialog.FolderName;
                string? error = AppDataService.ValidateAndSetStoragePath(newPath);
                if (error != null)
                {
                    StorageStatusText.Text = "❌ " + error;
                    return;
                }
                StoragePathText.Text = newPath;
                StorageStatusText.Text = "✅ Carpeta actualizada.";
            }
        }

        // =================================================================
        // IP PÚBLICA
        // =================================================================
        private async void GetPublicIp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                string ip = await client.GetStringAsync("https://api.ipify.org");
                PublicIpText.Text = ip;
                IpStatusText.Text = "Asegúrate de agregar esta IP en la configuración de tu API Key de Binance (API Management → Restricción de IP).";
            }
            catch (Exception ex)
            {
                IpStatusText.Text = "Error al obtener IP: " + ex.Message;
            }
        }

        // =================================================================
        // ACTUALIZACIÓN
        // =================================================================
        private void LoadVersionInfo()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            CurrentVersionText.Text = $"Versión instalada: {version?.ToString() ?? "1.0.0"}";
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateStatusText.Text = "Buscando actualizaciones...";

            try
            {
                var updater = new UpdateService();
                var release = await updater.CheckForUpdateAsync();

                if (release == null)
                {
                    UpdateStatusText.Text = "No hay actualizaciones disponibles o ya tienes la última versión.";
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                UpdateStatusText.Text = $"Nueva versión disponible: {release.Version} ({release.Size / 1024 / 1024} MB). Descargando...";

                string? msiPath = await updater.DownloadUpdateAsync(release, new Progress<int>(p =>
                {
                    Dispatcher.Invoke(() => UpdateProgressBar.Value = p);
                }));

                if (msiPath == null)
                {
                    UpdateStatusText.Text = "Error: no se pudo descargar o verificar la actualización.";
                    return;
                }

                UpdateStatusText.Text = "Actualización descargada. Instalando...";
                updater.ApplyUpdate(msiPath);
                MessageBox.Show("La actualización se instalará en segundo plano. La aplicación se reiniciará automáticamente.",
                    "Actualización", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                UpdateProgressBar.Visibility = Visibility.Collapsed;
            }
        }
    }
}
