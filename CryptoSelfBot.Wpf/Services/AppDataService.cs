using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CryptoSelfBot.Engine.Models;
using EngineMarketType = CryptoSelfBot.Engine.Models.MarketType;
using CryptoSelfBot.Wpf.Helpers;

namespace CryptoSelfBot.Wpf.Services
{
    public static partial class AppDataService
    {
        public static event Action? ActiveAccountChanged;
        public static event Action? RiskSettingsChanged;
        private static readonly string ConfigFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoSelfBot");
        private static readonly string ConfigFile = Path.Combine(ConfigFolder, "config.json");

        public static AppConfiguration Configuration { get; private set; } = new();

        static AppDataService()
        {
            Directory.CreateDirectory(ConfigFolder);
            Load();
        }

        public static void Load()
        {
            if (File.Exists(ConfigFile))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFile);
                    Configuration = JsonSerializer.Deserialize<AppConfiguration>(json) ?? CreateDefault();
                }
                catch { Configuration = CreateDefault(); }
            }
            else
            {
                Configuration = CreateDefault();
                Save();
            }
            // Migrar claves a DPAPI si es necesario
            MigrateAccountKeysToDpapi();
            EnsureFoldersExist();
        }

        public static void Save()
        {
            string json = JsonSerializer.Serialize(Configuration, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }

        private static AppConfiguration CreateDefault() => new()
        {
            Accounts = new List<BinanceAccount>
            {
                new BinanceAccount
                {
                    Label = "Principal",
                    ApiKey = "",
                    SecretKey = "",
                    NoWithdrawals = false,
                    Environment = TradingEnvironment.Live
                }
            },
            ActiveAccountIndex = 0,
            RiskSettings = new RiskSettings
            {
                AutoMaxTradeAmount = true,
                MaxTradeAmountUsdt = 0m,
                MaxOpenPositions = 3,
                MaxDailyLossPercent = 2m,
                CooldownSeconds = 10,
                EnableTrading = true
            },
            StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CryptoSelfBot"),
            Theme = "Themes/BinanceDark.xaml",
            MarketType = MarketType.Spot
        };

        public static BinanceAccount GetActiveAccount()
        {
            int idx = Configuration.ActiveAccountIndex;
            if (idx >= 0 && idx < Configuration.Accounts.Count)
                return Configuration.Accounts[idx];
            return Configuration.Accounts.FirstOrDefault() ?? new BinanceAccount();
        }

        public static void SetActiveAccount(int index)
        {
            if (index >= 0 && index < Configuration.Accounts.Count)
                Configuration.ActiveAccountIndex = index;
            Save();
            try { ActiveAccountChanged?.Invoke(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("ActiveAccountChanged handler error: " + ex.Message); }
        }

        public static void SaveAccounts(List<BinanceAccount> accounts, int activeIndex)
        {
            Configuration.Accounts = accounts;
            Configuration.ActiveAccountIndex = activeIndex;
            Save();
            try { ActiveAccountChanged?.Invoke(); } catch { }
        }

        public static void ExportAccounts(string filePath)
        {
            // Exportar cuentas con claves ya cifradas (no exponer claves en claro)
            var toExport = Configuration.Accounts.Select(a => new BinanceAccount
            {
                Label = a.Label,
                ApiKey = a.ApiKey, // ya cifrada por diseño
                SecretKey = a.SecretKey,
                Environment = a.Environment,
                NoWithdrawals = a.NoWithdrawals
            }).ToList();

            var json = JsonSerializer.Serialize(toExport, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public static void ImportAccounts(string filePath)
        {
            if (!File.Exists(filePath)) return;
            try
            {
                // Backup de config actual
                try
                {
                    string backupPath = ConfigFile + ".bak";
                    File.Copy(ConfigFile, backupPath, true);
                }
                catch (Exception bEx)
                {
                    System.Diagnostics.Debug.WriteLine("No se pudo crear backup antes de import: " + bEx.Message);
                }

                string json = File.ReadAllText(filePath);
                var imported = JsonSerializer.Deserialize<List<BinanceAccount>>(json);
                if (imported == null) return;

                // Asegurarse de que las claves importadas están cifradas; si parecen en claro, cifrarlas
                foreach (var acc in imported)
                {
                    if (!string.IsNullOrEmpty(acc.ApiKey))
                    {
                        try { var _ = EncryptionHelper.Decrypt(acc.ApiKey); }
                        catch { acc.ApiKey = EncryptionHelper.Encrypt(acc.ApiKey); }
                    }
                    if (!string.IsNullOrEmpty(acc.SecretKey))
                    {
                        try { var _ = EncryptionHelper.Decrypt(acc.SecretKey); }
                        catch { acc.SecretKey = EncryptionHelper.Encrypt(acc.SecretKey); }
                    }
                }

                Configuration.Accounts = imported;
                Configuration.ActiveAccountIndex = 0;
                Save();
                try { ActiveAccountChanged?.Invoke(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("ActiveAccountChanged handler error: " + ex.Message); }

                // Forzar migración futura por si hay elementos sin cifrar
                try { MigrateAccountKeysToDpapi(); } catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ImportAccounts error: " + ex.Message);
            }
        }

        public static void SaveRiskSettings(RiskSettings settings)
        {
            Configuration.RiskSettings = settings;
            Save();
            try { RiskSettingsChanged?.Invoke(); } catch { }
        }

        public static bool IsSystemDrive(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string? systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            string? targetRoot = Path.GetPathRoot(path);
            return string.Equals(systemRoot, targetRoot, StringComparison.OrdinalIgnoreCase);
        }

        public static string? ValidateAndSetStoragePath(string newPath)
        {
            if (IsSystemDrive(newPath))
                return "No se permite el disco C: para almacenar datos. Elija otra unidad.";
            Configuration.StoragePath = newPath;
            Save();
            EnsureFoldersExist();
            return null;
        }

        public static void EnsureFoldersExist()
        {
            string path = Configuration.StoragePath;
            if (string.IsNullOrEmpty(path)) return;
            Directory.CreateDirectory(Path.Combine(path, "Database"));
            Directory.CreateDirectory(Path.Combine(path, "Logs"));
            Directory.CreateDirectory(Path.Combine(path, "Exports"));
            Directory.CreateDirectory(Path.Combine(path, "NewsArchive"));
        }

        // Public wrapper para forzar la migración manual desde la UI
        public static void ForceMigrateAccountKeysToDpapi()
        {
            MigrateAccountKeysToDpapi();
        }

        public static void SetPauseLogging(bool pause)
        {
            Configuration.PauseLogging = pause;
            Save();
        }

        public static bool IsLoggingPaused()
        {
            return Configuration?.PauseLogging == true;
        }
    }

    public class AppConfiguration
    {
        public List<BinanceAccount> Accounts { get; set; } = new();
        public int ActiveAccountIndex { get; set; }
        public RiskSettings RiskSettings { get; set; } = new();
        public string StoragePath { get; set; } = "";
        public string Theme { get; set; } = "";
        public bool PauseLogging { get; set; } = false;
        public MarketType MarketType { get; set; } = MarketType.Spot;
        // Nueva configuración para integración y utilidades
        public string? PythonPath { get; set; } = null;
        public string? ApiEndpoint { get; set; } = null;
        public string? FREDApiKey { get; set; } = null;
        public bool EcbScraperEnabled { get; set; } = true;
    }

    public class BinanceAccount
    {
        public string Label { get; set; } = "Principal";
        public string ApiKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
        public TradingEnvironment Environment { get; set; } = TradingEnvironment.Live;
        // Si true, la cuenta tendrá deshabilitadas las retiradas programadas por seguridad.
        public bool NoWithdrawals { get; set; } = false;
    }

    // Migración helper
    public static partial class AppDataService
    {
        private static void MigrateAccountKeysToDpapi()
        {
            bool changed = false;
            if (Configuration?.Accounts == null) return;

            foreach (var acc in Configuration.Accounts)
            {
                // Si ApiKey/SecretKey están vacíos, omitir
                if (!string.IsNullOrEmpty(acc.ApiKey))
                {
                    try
                    {
                        // Intentar descifrar; si falla, asumimos que está en texto plano y ciframos
                        var _ = EncryptionHelper.Decrypt(acc.ApiKey);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Encrypting ApiKey during migration: " + ex.Message);
                        acc.ApiKey = EncryptionHelper.Encrypt(acc.ApiKey);
                        changed = true;
                    }
                }

                if (!string.IsNullOrEmpty(acc.SecretKey))
                {
                    try
                    {
                        var _ = EncryptionHelper.Decrypt(acc.SecretKey);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Encrypting SecretKey during migration: " + ex.Message);
                        acc.SecretKey = EncryptionHelper.Encrypt(acc.SecretKey);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Save();
            }
        }
    }
}
